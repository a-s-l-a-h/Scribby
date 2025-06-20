using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel; // For FileSystem
using Microsoft.Maui.Controls;
using System.Diagnostics; // For Debug.WriteLine

namespace ScribbyApp.Views
{
    public static class NativeLlmMethods // Embedded P/Invoke class
    {
        // For Windows, the DLL name is just "chat" (without .dll)
        // For other platforms, it might be "libchat.so" or "libchat.dylib"
        // MSBuild <Link>chat.dll</Link> ensures it's named chat.dll in output.
        internal const string DllName = "chat";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void TokenCallbackDelegate(IntPtr tokenPtr);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void registerTokenCallback(TokenCallbackDelegate callback);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern bool initializeLlama(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string model_path,
            int n_ctx_param,
            int ngl,
            float temp,
            float min_p,
            int n_threads_py, // In your C++ these might be n_threads
            int n_threads_batch_py // and n_threads_batch
        );

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void generateResponse([MarshalAs(UnmanagedType.LPUTF8Str)] string user_input, int max_tokens);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void stopGeneration();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void clearConversation();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void freeLlama();
    }

    public partial class ChatLLMPage : ContentPage
    {
        private NativeLlmMethods.TokenCallbackDelegate _tokenCallbackInstance;
        private bool _isLlmInitialized = false;
        private string _modelPathInAppDirectory = string.Empty;
        private const string ModelFileName = "qwen.gguf";

        private CancellationTokenSource _generationCts;
        private Task _generationTask;
        private readonly StringBuilder _currentAiResponseBuilder = new StringBuilder();


        public ChatLLMPage()
        {
            InitializeComponent();
            _tokenCallbackInstance = new NativeLlmMethods.TokenCallbackDelegate(ManagedTokenCallback);
            // Set initial UI states
            ChatInputEntry.IsEnabled = false;
            SendButton.IsEnabled = false;
            StopButton.IsEnabled = false;
            ClearButton.IsEnabled = false;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            // You might want to auto-initialize or provide a clear button
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
                    NativeLlmMethods.stopGeneration();
                    // Give it a moment to stop
                    Task.Run(async () => await _generationTask.ConfigureAwait(false)).Wait(500);
                }
                Debug.WriteLine("ChatLLMPage: OnDisappearing - Freeing Llama.");
                NativeLlmMethods.freeLlama();
                _isLlmInitialized = false;
                UpdateStatus("LLM resources released.");
                Debug.WriteLine("ChatLLMPage: OnDisappearing - LLM resources freed.");
            }
        }

        private async Task<string> CopyModelToAppDataAsync()
        {
            var appDataDir = FileSystem.AppDataDirectory;
            var destinationPath = Path.Combine(appDataDir, ModelFileName);

            // Check if model already exists to avoid re-copying
            if (File.Exists(destinationPath))
            {
                UpdateStatus($"Model '{ModelFileName}' found in app data directory.");
                Debug.WriteLine($"Model '{ModelFileName}' found at {destinationPath}");
                return destinationPath;
            }

            UpdateStatus($"Copying '{ModelFileName}' to app data directory...");
            Debug.WriteLine($"Copying '{ModelFileName}' from package to {destinationPath}");
            try
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync(ModelFileName);
                using var newStream = File.Create(destinationPath);
                await stream.CopyToAsync(newStream);
                UpdateStatus($"Model '{ModelFileName}' copied successfully.");
                Debug.WriteLine($"Model '{ModelFileName}' copied to {destinationPath}");
                return destinationPath;
            }
            catch (FileNotFoundException fnfEx)
            {
                var errorMsg = $"Error: Model file '{ModelFileName}' not found in app package (Resources/Raw). {fnfEx.Message}";
                UpdateStatus(errorMsg);
                Debug.WriteLine(errorMsg);
                await DisplayAlert("Error", errorMsg, "OK");
                return null;
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error copying model file: {ex.Message}";
                UpdateStatus(errorMsg);
                Debug.WriteLine(errorMsg);
                await DisplayAlert("Error", errorMsg, "OK");
                return null;
            }
        }

        private async void InitializeLlmButton_Clicked(object sender, EventArgs e)
        {
            if (_isLlmInitialized)
            {
                UpdateStatus("LLM is already initialized.");
                return;
            }

            InitializeButton.IsEnabled = false;
            UpdateStatus("Initializing LLM... This may take a moment.");

            _modelPathInAppDirectory = await CopyModelToAppDataAsync();
            if (string.IsNullOrEmpty(_modelPathInAppDirectory))
            {
                UpdateStatus("Failed to prepare model file. Initialization aborted.");
                InitializeButton.IsEnabled = true;
                return;
            }

            // Initialization can be lengthy, run on a background thread
            await Task.Run(() =>
            {
                try
                {
                    Debug.WriteLine("C#: Registering token callback...");
                    NativeLlmMethods.registerTokenCallback(_tokenCallbackInstance);

                    int nCtx = 2048;
                    int nGpuLayers = 0; // Set to 0 for CPU, >0 for GPU layers if your llama.cpp build supports it
                    float temperature = 0.7f;
                    float minP = 0.1f;
                    // Adjust thread counts based on your C++ implementation's parameters
                    // (n_threads_py / n_threads_batch_py from your example)
                    // If your C++ uses `n_threads` and `n_threads_batch`, adjust names
                    int nThreads = Environment.ProcessorCount > 1 ? Environment.ProcessorCount / 2 : 1; // Example
                    int nThreadsBatch = Environment.ProcessorCount > 1 ? Environment.ProcessorCount / 2 : 1; // Example

                    Debug.WriteLine($"C#: Initializing Llama with model: {_modelPathInAppDirectory}");
                    bool success = NativeLlmMethods.initializeLlama(
                        _modelPathInAppDirectory, nCtx, nGpuLayers, temperature, minP, nThreads, nThreadsBatch
                    );

                    if (success)
                    {
                        _isLlmInitialized = true;
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            UpdateStatus("LLM Initialized successfully. Ready to chat.");
                            ChatInputEntry.IsEnabled = true;
                            SendButton.IsEnabled = true;
                            ClearButton.IsEnabled = true;
                            InitializeButton.Text = "LLM Initialized"; // Update button text
                            // InitializeButton.IsEnabled remains false or you can change its function
                        });
                        Debug.WriteLine("C#: Llama initialized successfully.");
                    }
                    else
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            UpdateStatus("Failed to initialize LLM. Check C++ logs/console.");
                            InitializeButton.IsEnabled = true; // Allow retry
                        });
                        Debug.WriteLine("C#: Failed to initialize Llama.");
                    }
                }
                catch (DllNotFoundException dllEx)
                {
                    var errorMsg = $"Error: DLL '{NativeLlmMethods.DllName}' or one of its dependencies not found. {dllEx.Message}";
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        UpdateStatus(errorMsg);
                        InitializeButton.IsEnabled = true;
                    });
                    Debug.WriteLine(errorMsg);
                }
                catch (Exception ex)
                {
                    var errorMsg = $"Initialization error: {ex.Message}";
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        UpdateStatus(errorMsg);
                        InitializeButton.IsEnabled = true;
                    });
                    Debug.WriteLine(errorMsg);
                }
            });
        }

        private void ManagedTokenCallback(IntPtr tokenPtr)
        {
            try
            {
                string token = Marshal.PtrToStringUTF8(tokenPtr);
                if (token != null)
                {
                    _currentAiResponseBuilder.Append(token);
                    // Update editor incrementally on the UI thread
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        ChatOutputEditor.Text += token;
                        // Auto-scroll
                        await Task.Delay(10); // Small delay to allow UI to update
                        await ChatScrollView.ScrollToAsync(ChatOutputEditor, ScrollToPosition.End, false);

                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"C# Callback Error: {ex.Message}");
                MainThread.BeginInvokeOnMainThread(() => UpdateStatus($"Callback error: {ex.Message}"));
            }
        }

        private async void SendButton_Clicked(object sender, EventArgs e)
        {
            if (!_isLlmInitialized || _generationTask != null && !_generationTask.IsCompleted)
            {
                UpdateStatus("LLM not ready or generation in progress.");
                return;
            }

            string userInput = ChatInputEntry.Text?.Trim();
            if (string.IsNullOrEmpty(userInput)) return;

            ChatInputEntry.Text = ""; // Clear input
            ChatOutputEditor.Text += $"You: {userInput}\n";
            ChatOutputEditor.Text += "AI: ";
            _currentAiResponseBuilder.Clear();


            SendButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            UpdateStatus("Generating response...");

            _generationCts = new CancellationTokenSource();
            _generationTask = Task.Run(() =>
            {
                try
                {
                    NativeLlmMethods.generateResponse(userInput, 1024); // max_tokens
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"C# Error during generateResponse P/Invoke: {ex.Message}");
                    MainThread.BeginInvokeOnMainThread(() => UpdateStatus($"Generation error: {ex.Message}"));
                }
                finally
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        ChatOutputEditor.Text += "\n"; // New line after AI response
                        UpdateStatus("Generation finished.");
                        SendButton.IsEnabled = true;
                        StopButton.IsEnabled = false;
                        await Task.Delay(10);
                        await ChatScrollView.ScrollToAsync(ChatOutputEditor, ScrollToPosition.End, true);
                    });
                    Debug.WriteLine("C#: generateResponse call finished from C# perspective.");
                }
            }, _generationCts.Token);

            try
            {
                await _generationTask;
            }
            catch (TaskCanceledException)
            {
                MainThread.BeginInvokeOnMainThread(() => UpdateStatus("Generation stopped by user."));
                Debug.WriteLine("C#: Generation task was cancelled.");
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(() => UpdateStatus($"Error during generation: {ex.Message}"));
                Debug.WriteLine($"C#: Error awaiting generation task: {ex.Message}");
            }
            finally
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    SendButton.IsEnabled = true;
                    StopButton.IsEnabled = false;
                });
            }
        }

        private void StopButton_Clicked(object sender, EventArgs e)
        {
            if (_generationTask != null && !_generationTask.IsCompleted)
            {
                UpdateStatus("Stopping generation...");
                Debug.WriteLine("C#: Sending stop signal to C++...");
                NativeLlmMethods.stopGeneration();
                _generationCts?.Cancel(); // Also cancel the C# task
            }
            else
            {
                UpdateStatus("No active generation to stop.");
            }
        }

        private void ClearButton_Clicked(object sender, EventArgs e)
        {
            if (!_isLlmInitialized) return;

            if (_generationTask != null && !_generationTask.IsCompleted)
            {
                UpdateStatus("Stopping active generation before clearing...");
                NativeLlmMethods.stopGeneration();
                _generationCts?.Cancel();
                // Consider waiting for task completion or make it async
            }
            NativeLlmMethods.clearConversation();
            ChatOutputEditor.Text = "";
            UpdateStatus("Conversation history cleared.");
            Debug.WriteLine("C#: Conversation history cleared.");
        }

        private void UpdateStatus(string message)
        {
            MainThread.BeginInvokeOnMainThread(() => StatusLabel.Text = $"Status: {message}");
        }
    }
}