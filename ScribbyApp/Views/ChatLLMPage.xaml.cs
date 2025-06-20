using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text; // Using System.Text explicitly
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using System.Diagnostics;

#if ANDROID
// Using alias for Java.Lang to avoid widespread ambiguity.
using JavaSystem = Java.Lang.JavaSystem;
using JavaUnsatisfiedLinkError = Java.Lang.UnsatisfiedLinkError;
#endif

namespace ScribbyApp.Views
{
    public static class NativeLlmMethods
    {
        internal const string DllName = "chat";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void TokenCallbackDelegate(IntPtr tokenPtr);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "registerTokenCallback")]
        public static extern void RegisterTokenCallback_PInvoke(TokenCallbackDelegate callback);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "initializeLlama")]
        public static extern bool InitializeLlama_PInvoke(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string model_path,
            int n_ctx_param, int ngl_param, float temp_param, float min_p_param,
            int n_threads_param, int n_threads_batch_param);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "generateResponse")]
        public static extern void GenerateResponse_PInvoke([MarshalAs(UnmanagedType.LPUTF8Str)] string user_input, int max_tokens);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "stopGeneration")]
        public static extern void StopGeneration_PInvoke();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "clearConversation")]
        public static extern void ClearConversation_PInvoke();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "freeLlama")]
        public static extern void FreeLlama_PInvoke();
    }

    public partial class ChatLLMPage : ContentPage
    {
        private NativeLlmMethods.TokenCallbackDelegate _tokenCallbackInstance;
        private bool _isLlmInitialized = false;
        private string _modelPathInAppDirectory = string.Empty;
        private const string ModelFileName = "qwen.gguf";
        private CancellationTokenSource? _generationCts;
        private Task? _generationTask;
        private readonly StringBuilder _currentAiResponseBuilder = new StringBuilder();
        private bool _nativeLibraryPreloadAttempted = false;
        private bool _nativeLibraryPreloadSuccessful = false;

        public ChatLLMPage()
        {
            InitializeComponent();
            _tokenCallbackInstance = new NativeLlmMethods.TokenCallbackDelegate(ManagedTokenCallback);
            SetUiForNoLlm();
        }

        private void SetUiForNoLlm()
        {
            ChatInputEntry.IsEnabled = false;
            SendButton.IsEnabled = false;
            StopButton.IsEnabled = false;
            ClearButton.IsEnabled = false;
            InitializeButton.IsEnabled = true; // Allow attempt to initialize
            InitializeButton.Text = "Load LLM Model & Initialize";
        }

        private void SetUiForLlmInitialized()
        {
            ChatInputEntry.IsEnabled = true;
            SendButton.IsEnabled = true;
            ClearButton.IsEnabled = true;
            InitializeButton.Text = "LLM Initialized";
            InitializeButton.IsEnabled = false; // Or change its function
            StopButton.IsEnabled = false;
        }


        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (!_nativeLibraryPreloadAttempted)
            {
                await AttemptNativeLibraryPreloadAsync();
            }
        }

        // This method attempts to load libraries using Android's JavaSystem.LoadLibrary
        // to get early feedback from the OS linker.
        private async Task AttemptNativeLibraryPreloadAsync()
        {
            _nativeLibraryPreloadAttempted = true;
            _nativeLibraryPreloadSuccessful = false; // Assume failure until success

#if ANDROID
            UpdateStatus("Pre-loading native libraries (Android)...");
            Debug.WriteLine("NATIVE_LOAD: Attempting Android native library pre-load sequence.");

            // Order of loading dependencies can sometimes matter.
            // Load deepest dependencies first if known, otherwise alphabetical or logical grouping.
            string[] LIBRARIES_TO_PRELOAD = { "omp","ggml-base", "ggml-cpu", "ggml", "llama", "chat" };

            foreach (var libName in LIBRARIES_TO_PRELOAD)
            {
                try
                {
                    Debug.WriteLine($"NATIVE_LOAD: Attempting JavaSystem.LoadLibrary(\"{libName}\")");
                    JavaSystem.LoadLibrary(libName);
                    Debug.WriteLine($"NATIVE_LOAD: JavaSystem.LoadLibrary(\"{libName}\") call completed without immediate Java exception.");
                    // If this is the main "chat" library, we can tentatively say preload was successful
                    if (libName == "chat")
                    {
                        _nativeLibraryPreloadSuccessful = true;
                        UpdateStatus($"Native library '{libName}' pre-load SUCCESSFUL via JavaSystem.");
                    }
                }
                catch (JavaUnsatisfiedLinkError unsatLinkErr)
                {
                    var errorMsg = $"CRITICAL: JavaSystem.LoadLibrary FAILED for '{libName}'. Message: {unsatLinkErr.Message}. This usually means the .so file is missing from APK, for wrong ABI, corrupted, or has missing NATIVE dependencies.";
                    Debug.WriteLine($"NATIVE_LOAD_ERROR: {errorMsg}");
                    UpdateStatus(errorMsg);
                    await DisplayAlert("Native Library Load Error", errorMsg, "OK");
                    _nativeLibraryPreloadSuccessful = false;
                    return; // Stop further attempts if a critical library fails
                }
                catch (System.Exception ex) // Catch any other C# exceptions during the load attempt
                {
                    var errorMsg = $"CRITICAL: System.Exception during JavaSystem.LoadLibrary(\"{libName}\"). Message: {ex.Message}";
                    Debug.WriteLine($"NATIVE_LOAD_EXCEPTION: {errorMsg}");
                    UpdateStatus(errorMsg);
                    await DisplayAlert("Native Library Load Exception", errorMsg, "OK");
                    _nativeLibraryPreloadSuccessful = false;
                    return;
                }
            }

            if (_nativeLibraryPreloadSuccessful)
            {
                Debug.WriteLine("NATIVE_LOAD: All specified native libraries pre-loaded successfully via JavaSystem.LoadLibrary.");
                UpdateStatus("All native libraries pre-loaded successfully (Android).");
            }
            else
            {
                Debug.WriteLine("NATIVE_LOAD: Native library pre-load sequence completed, but 'chat' library did not confirm success via JavaSystem.");
                // This case should ideally be caught by the chat library specific error above.
            }

#else
            // On non-Android platforms, P/Invoke will be the first load attempt.
            // We can consider the "preload" as conceptually successful for P/Invoke to proceed.
            _nativeLibraryPreloadSuccessful = true;
            UpdateStatus("Native library pre-load not applicable for this platform or already handled by P/Invoke.");
            Debug.WriteLine("NATIVE_LOAD: Pre-load step skipped for non-Android platform.");
#endif
        }


        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            if (_isLlmInitialized)
            {
                UpdateStatus("Page disappearing. Releasing LLM resources...");
                Debug.WriteLine("ChatLLMPage: OnDisappearing - Stopping generation if active.");
                if (_generationTask != null && !_generationTask.IsCompleted)
                {
                    NativeLlmMethods.StopGeneration_PInvoke();
                    _generationCts?.Cancel();
                    Task.Run(async () => await _generationTask.ConfigureAwait(false)).Wait(TimeSpan.FromMilliseconds(500));
                }
                Debug.WriteLine("ChatLLMPage: OnDisappearing - Freeing Llama.");
                NativeLlmMethods.FreeLlama_PInvoke();
                _isLlmInitialized = false;
                UpdateStatus("LLM resources released.");
                Debug.WriteLine("ChatLLMPage: OnDisappearing - LLM resources freed.");
            }
        }

        private async Task<string?> CopyModelToAppDataAsync()
        {
            var appDataDir = FileSystem.AppDataDirectory;
            var destinationPath = Path.Combine(appDataDir, ModelFileName);

            if (File.Exists(destinationPath))
            {
                // UpdateStatus($"Model '{ModelFileName}' found in app data directory."); // Can be noisy
                Debug.WriteLine($"Model '{ModelFileName}' found at {destinationPath}");
                return destinationPath;
            }

            UpdateStatus($"Copying '{ModelFileName}' to app data directory...");
            Debug.WriteLine($"Copying '{ModelFileName}' from package (Resources/Raw/{ModelFileName}) to {destinationPath}");
            try
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync(ModelFileName);
                if (stream == null) { throw new FileNotFoundException($"Model file '{ModelFileName}' not found in app package."); }
                using var newStream = File.Create(destinationPath);
                await stream.CopyToAsync(newStream);
                UpdateStatus($"Model '{ModelFileName}' copied successfully.");
                Debug.WriteLine($"Model '{ModelFileName}' copied to {destinationPath}");
                return destinationPath;
            }
            catch (FileNotFoundException fnfEx)
            {
                var errorMsg = $"Error: Model file '{ModelFileName}' not found in app package (Resources/Raw). Ensure it's there and Build Action is MauiAsset. Details: {fnfEx.Message}";
                UpdateStatus(errorMsg); Debug.WriteLine(errorMsg); await DisplayAlert("Model Error", errorMsg, "OK"); return null;
            }
            catch (System.Exception ex)
            {
                var errorMsg = $"Error copying model file: {ex.Message}";
                UpdateStatus(errorMsg); Debug.WriteLine(errorMsg); await DisplayAlert("File Error", errorMsg, "OK"); return null;
            }
        }

        private async void InitializeLlmButton_Clicked(object sender, EventArgs e)
        {
            if (_isLlmInitialized) { UpdateStatus("LLM is already initialized."); return; }

            // Ensure preload was attempted and (conditionally) successful
            if (!_nativeLibraryPreloadAttempted)
            {
                UpdateStatus("Native library pre-load not yet attempted. Please wait or re-enter page.");
                await AttemptNativeLibraryPreloadAsync(); // Try again
            }

            // On Android, if JavaSystem.LoadLibrary failed, P/Invoke will also fail.
            // On other platforms, _nativeLibraryPreloadSuccessful is true by default.
            if (!_nativeLibraryPreloadSuccessful)
            {
                UpdateStatus("Native library pre-load failed. Cannot proceed with P/Invoke initialization. Check logs.");
                Debug.WriteLine("INIT_LLM_ABORT: Aborting P/Invoke initialization due to native library pre-load failure.");
                await DisplayAlert("Initialization Error", "Critical native libraries failed to load during pre-load. Cannot initialize LLM.", "OK");
                SetUiForNoLlm(); // Ensure UI reflects failure
                return;
            }

            InitializeButton.IsEnabled = false;
            UpdateStatus("P/INVOKE: Initializing LLM...");

            // Stage 1: P/Invoke registerTokenCallback
            try
            {
                Debug.WriteLine("P_INVOKE_STAGE_1: Attempting NativeLlmMethods.RegisterTokenCallback_PInvoke...");
                NativeLlmMethods.RegisterTokenCallback_PInvoke(_tokenCallbackInstance);
                Debug.WriteLine("P_INVOKE_STAGE_1: NativeLlmMethods.RegisterTokenCallback_PInvoke call SUCCEEDED (or didn't throw DllNotFoundException here).");
                UpdateStatus("P/Invoke Stage 1 (registerTokenCallback) passed.");
            }
            catch (DllNotFoundException dllEx)
            {
                var errorMsg = $"P/Invoke DllNotFoundException (Stage 1 - registerTokenCallback): '{dllEx.Message}'. This means .NET could not load '{NativeLlmMethods.DllName}' (libchat.so). This can happen if JavaSystem.LoadLibrary seemed to pass but P/Invoke still fails (rare), or on non-Android if lib is missing.";
                Debug.WriteLine($"CRITICAL_PINVOKE_ERROR_S1: {errorMsg}"); UpdateStatus(errorMsg); SetUiForNoLlm(); return;
            }
            catch (System.Exception ex)
            {
                var errorMsg = $"P/Invoke System.Exception (Stage 1 - registerTokenCallback): {ex.GetType().Name} - {ex.Message}";
                Debug.WriteLine($"PINVOKE_ERROR_S1: {errorMsg}\n{ex.StackTrace}"); UpdateStatus(errorMsg); SetUiForNoLlm(); return;
            }

            _modelPathInAppDirectory = (await CopyModelToAppDataAsync()) ?? string.Empty;
            if (string.IsNullOrEmpty(_modelPathInAppDirectory))
            {
                UpdateStatus("Failed to prepare model file. Initialization aborted."); SetUiForNoLlm(); return;
            }

            UpdateStatus("Model prepared. P/INVOKE: Calling initializeLlama...");

            // Stage 2: P/Invoke initializeLlama (on background thread)
            await Task.Run(() =>
            {
                try
                {
                    int nCtx = 2048; int nGpuLayers = 0; float temperature = 0.7f; float minP = 0.1f;
                    int nThreads = System.Math.Max(1, Environment.ProcessorCount / 2);
                    int nThreadsBatch = System.Math.Max(1, Environment.ProcessorCount / 2);

                    Debug.WriteLine($"P_INVOKE_STAGE_2: Attempting NativeLlmMethods.InitializeLlama_PInvoke with model: {_modelPathInAppDirectory}");
                    bool success = NativeLlmMethods.InitializeLlama_PInvoke(
                        _modelPathInAppDirectory, nCtx, nGpuLayers, temperature, minP, nThreads, nThreadsBatch);
                    Debug.WriteLine($"P_INVOKE_STAGE_2: NativeLlmMethods.InitializeLlama_PInvoke returned: {success}");

                    if (success)
                    {
                        _isLlmInitialized = true;
                        MainThread.BeginInvokeOnMainThread(() => {
                            UpdateStatus("LLM Initialized successfully."); SetUiForLlmInitialized();
                        });
                    }
                    else
                    {
                        MainThread.BeginInvokeOnMainThread(() => {
                            UpdateStatus("initializeLlama P/Invoke succeeded but native code returned 'false'. Check native logs."); SetUiForNoLlm();
                        });
                        Debug.WriteLine("P_INVOKE_STAGE_2_FAIL_NATIVE: InitializeLlama_PInvoke returned 'false'.");
                    }
                }
                catch (DllNotFoundException dllEx)
                {
                    var errorMsg = $"P/Invoke DllNotFoundException (Stage 2 - initializeLlama): '{dllEx.Message}'. This could mean a DEEPER native dependency of 'libchat.so' (needed for initializeLlama) failed to load.";
                    Debug.WriteLine($"CRITICAL_PINVOKE_ERROR_S2_DLLNOTFOUND: {errorMsg}");
                    MainThread.BeginInvokeOnMainThread(() => { UpdateStatus(errorMsg); SetUiForNoLlm(); });
                }
                catch (EntryPointNotFoundException epnfEx)
                {
                    var errorMsg = $"P/Invoke EntryPointNotFoundException (Stage 2 - initializeLlama): '{epnfEx.Message}'. Library '{NativeLlmMethods.DllName}' loaded, but function signature mismatches C++.";
                    Debug.WriteLine($"PINVOKE_ERROR_S2_ENTRYPOINT: {errorMsg}");
                    MainThread.BeginInvokeOnMainThread(() => { UpdateStatus(errorMsg); SetUiForNoLlm(); });
                }
                catch (System.Exception ex)
                {
                    var errorMsg = $"P/Invoke System.Exception (Stage 2 - initializeLlama): {ex.GetType().Name} - {ex.Message}";
                    Debug.WriteLine($"PINVOKE_ERROR_S2: {errorMsg}\n{ex.StackTrace}");
                    MainThread.BeginInvokeOnMainThread(() => { UpdateStatus(errorMsg); SetUiForNoLlm(); });
                }
            });
        }

        private void ManagedTokenCallback(IntPtr tokenPtr)
        {
            try
            {
                if (tokenPtr == IntPtr.Zero) return;
                string? token = Marshal.PtrToStringUTF8(tokenPtr); // CS8600 handled by if (token != null)
                if (token != null)
                {
                    MainThread.BeginInvokeOnMainThread(async () => {
                        ChatOutputEditor.Text += token;
                        await Task.Delay(10);
                        await ChatScrollView.ScrollToAsync(ChatOutputEditor, ScrollToPosition.End, false);
                    });
                }
            }
            catch (System.Exception ex) { Debug.WriteLine($"C# Token Callback Error: {ex.Message}"); MainThread.BeginInvokeOnMainThread(() => UpdateStatus($"Callback error: {ex.Message}")); }
        }

        private async void SendButton_Clicked(object sender, EventArgs e)
        {
            if (!_isLlmInitialized || (_generationTask != null && !_generationTask.IsCompleted))
            { UpdateStatus(!_isLlmInitialized ? "LLM not initialized." : "Generation already in progress."); return; }
            string? userInput = ChatInputEntry.Text?.Trim();
            if (string.IsNullOrEmpty(userInput)) return;
            ChatInputEntry.Text = ""; ChatOutputEditor.Text += $"You: {userInput}\nAI: ";
            _currentAiResponseBuilder.Clear();
            SendButton.IsEnabled = false; ChatInputEntry.IsEnabled = false; StopButton.IsEnabled = true;
            UpdateStatus("Generating response...");
            _generationCts = new CancellationTokenSource();
            _generationTask = Task.Run(() => {
                try { NativeLlmMethods.GenerateResponse_PInvoke(userInput, 1024); } // userInput is non-null here
                catch (System.Exception ex) { Debug.WriteLine($"P/Invoke GenerateResponse Error: {ex.Message}"); MainThread.BeginInvokeOnMainThread(() => UpdateStatus($"Generation P/Invoke error: {ex.Message}")); }
                finally
                {
                    MainThread.BeginInvokeOnMainThread(async () => {
                        if (_generationCts != null && !_generationCts.IsCancellationRequested) { ChatOutputEditor.Text += "\n"; }
                        UpdateStatus("Generation finished/stopped.");
                        SendButton.IsEnabled = true; ChatInputEntry.IsEnabled = true; StopButton.IsEnabled = false;
                        await Task.Delay(10); await ChatScrollView.ScrollToAsync(ChatOutputEditor, ScrollToPosition.End, true);
                    });
                    Debug.WriteLine("Native generateResponse call finished (from C# task perspective).");
                }
            }, _generationCts.Token);
            try { await _generationTask; }
            catch (TaskCanceledException) { MainThread.BeginInvokeOnMainThread(() => UpdateStatus("Generation stopped by user.")); Debug.WriteLine("C# Generation task cancelled."); }
            catch (System.Exception ex) { MainThread.BeginInvokeOnMainThread(() => UpdateStatus($"Generation task error: {ex.Message}")); Debug.WriteLine($"C# Awaiting generation task error: {ex.Message}"); }
        }

        private void StopButton_Clicked(object sender, EventArgs e)
        {
            if (_generationTask != null && !_generationTask.IsCompleted)
            { UpdateStatus("Stopping generation..."); Debug.WriteLine("C#: Sending stop signal to native code..."); NativeLlmMethods.StopGeneration_PInvoke(); _generationCts?.Cancel(); }
            else { UpdateStatus("No active generation to stop."); }
        }

        private void ClearButton_Clicked(object sender, EventArgs e)
        {
            if (!_isLlmInitialized) return;
            if (_generationTask != null && !_generationTask.IsCompleted)
            { UpdateStatus("Stopping active generation before clearing..."); NativeLlmMethods.StopGeneration_PInvoke(); _generationCts?.Cancel(); }
            NativeLlmMethods.ClearConversation_PInvoke(); ChatOutputEditor.Text = "";
            UpdateStatus("Conversation history cleared."); Debug.WriteLine("C#: Conversation history cleared.");
        }

        private void UpdateStatus(string message)
        {
            MainThread.BeginInvokeOnMainThread(() => StatusLabel.Text = $"Status: {message}");
        }
    }
}