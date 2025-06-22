using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using System.Diagnostics;
using ScribbyApp.Services;
using NLua;
using System.Collections.Generic;

#if ANDROID
using JavaSystem = Java.Lang.JavaSystem;
using JavaUnsatisfiedLinkError = Java.Lang.UnsatisfiedLinkError;
#endif

namespace ScribbyApp.Views
{
    public record ChatMessage(string Role, string Content);

    public static class NativeLlmMethods
    {
        // This static class remains unchanged
        internal const string DllName = "chat";
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void TokenCallbackDelegate(IntPtr tokenPtr);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "registerTokenCallback")] public static extern void RegisterTokenCallback_PInvoke(TokenCallbackDelegate callback);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "initializeLlama")] public static extern bool InitializeLlama_PInvoke([MarshalAs(UnmanagedType.LPUTF8Str)] string model_path, int n_ctx_param, int ngl_param, float temp_param, float min_p_param, int n_threads_param, int n_threads_batch_param);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "generateResponse")] public static extern void GenerateResponse_PInvoke([MarshalAs(UnmanagedType.LPUTF8Str)] string user_input, int max_tokens);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "stopGeneration")] public static extern void StopGeneration_PInvoke();
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "clearConversation")] public static extern void ClearConversation_PInvoke();
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "freeLlama")] public static extern void FreeLlama_PInvoke();
    }

    public partial class ChatLLMPage : ContentPage
    {
        private readonly NativeLlmMethods.TokenCallbackDelegate _tokenCallbackInstance;
        private bool _isLlmInitialized = false;
        private string _modelPathInAppDirectory = string.Empty;
        private const string ModelFileName = "qwen.gguf";
        private CancellationTokenSource? _generationCts;
        private Task? _generationTask;
        private readonly StringBuilder _currentAiResponseBuilder = new StringBuilder();
        private bool _nativeLibraryPreloadAttempted = false;
        private bool _nativeLibraryPreloadSuccessful = false;
        private Label? _streamingResponseLabel;

        private readonly BluetoothService _bluetoothService;
        private CancellationTokenSource? _scriptCts;

        private readonly List<ChatMessage> _chatHistory = new();

        public ChatLLMPage(BluetoothService bluetoothService)
        {
            InitializeComponent();
            _bluetoothService = bluetoothService;
            _tokenCallbackInstance = new NativeLlmMethods.TokenCallbackDelegate(ManagedTokenCallback);
            SetUiForNoLlm();
        }

        #region Page Lifecycle and UI State Management
        // This region is unchanged
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (!_nativeLibraryPreloadAttempted && !_isLlmInitialized)
            {
                await AttemptNativeLibraryPreloadAsync();
            }
        }
        protected override void OnDisappearing() { base.OnDisappearing(); }
        private void SetUiForNoLlm()
        {
            ChatInputEntry.IsEnabled = false;
            SendButton.IsEnabled = false;
            StopGenerationButton.IsEnabled = false;
            DeinitializeButton.IsEnabled = false;
            AbortScriptButton.IsEnabled = false;
            SendSButton.IsEnabled = false;
            InitializeButton.IsEnabled = true;
            InitializeButton.IsVisible = true;
        }
        private void SetUiForLlmInitialized()
        {
            InitializeButton.IsVisible = false;
            DeinitializeButton.IsEnabled = true;
            UpdateUiForInteractionState(isGenerating: false, isScriptRunning: false);
        }
        private void UpdateUiForInteractionState(bool isGenerating, bool isScriptRunning)
        {
            if (!_isLlmInitialized) { SetUiForNoLlm(); return; }
            bool isBusy = isGenerating || isScriptRunning;
            ChatInputEntry.IsEnabled = !isBusy;
            SendButton.IsEnabled = !isBusy;
            StopGenerationButton.IsEnabled = isGenerating;
            DeinitializeButton.IsEnabled = !isBusy;
            AbortScriptButton.IsEnabled = isScriptRunning;
            SendSButton.IsEnabled = _bluetoothService.IsConnected && !isBusy;
            foreach (var codeBlockBorder in ChatHistoryLayout.Children.OfType<Border>())
            {
                if (codeBlockBorder.Content is not Grid mainGrid) continue;
                var buttonContainer = mainGrid.Children.OfType<HorizontalStackLayout>().FirstOrDefault();
                if (buttonContainer == null) continue;
                var copyButton = buttonContainer.Children.OfType<Button>().FirstOrDefault(b => b.Style == (Style)Resources["CodeBlockCopyStyle"]);
                var runButton = buttonContainer.Children.OfType<Button>().FirstOrDefault(b => b.Style == (Style)Resources["CodeBlockRunStyle"]);
                if (copyButton != null) copyButton.IsEnabled = !isBusy;
                if (runButton != null) { runButton.IsEnabled = !isBusy; }
            }
        }
        #endregion

        #region Native Library Loading & LLM Initialization / De-initialization
        // This region is unchanged
        private async Task AttemptNativeLibraryPreloadAsync()
        {
            _nativeLibraryPreloadAttempted = true;
            _nativeLibraryPreloadSuccessful = false;
#if ANDROID
            UpdateStatus("Pre-loading native libraries (Android)...");
            string[] LIBRARIES_TO_PRELOAD = { "omp", "ggml-base", "ggml-cpu", "ggml", "llama", "chat" };
            foreach (var libName in LIBRARIES_TO_PRELOAD)
            {
                try { JavaSystem.LoadLibrary(libName); if (libName == "chat") _nativeLibraryPreloadSuccessful = true; }
                catch (Exception ex) { var errorMsg = $"CRITICAL: LoadLibrary FAILED for '{libName}'. {ex.Message}"; UpdateStatus(errorMsg); await DisplayAlert("Native Library Load Error", errorMsg, "OK"); _nativeLibraryPreloadSuccessful = false; return; }
            }
            if (_nativeLibraryPreloadSuccessful) UpdateStatus("All native libraries pre-loaded successfully.");
#else
            _nativeLibraryPreloadSuccessful = true; UpdateStatus("Pre-load step not required for this platform.");
#endif
        }
        private async Task<string?> CopyModelToAppDataAsync()
        {
            var appDataDir = FileSystem.AppDataDirectory;
            var destinationPath = Path.Combine(appDataDir, ModelFileName);
            if (File.Exists(destinationPath)) return destinationPath;
            UpdateStatus($"Copying '{ModelFileName}'...");
            try
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync(ModelFileName); if (stream == null) throw new FileNotFoundException($"Model file '{ModelFileName}' not found.");
                using var newStream = File.Create(destinationPath); await stream.CopyToAsync(newStream); return destinationPath;
            }
            catch (Exception ex) { var errorMsg = $"Error copying model file: {ex.Message}"; UpdateStatus(errorMsg); await DisplayAlert("File Error", errorMsg, "OK"); return null; }
        }
        private async void InitializeLlmButton_Clicked(object? sender, EventArgs e)
        {
            if (_isLlmInitialized) { UpdateStatus("LLM is already initialized."); return; }
            if (!_nativeLibraryPreloadSuccessful) { UpdateStatus("Native library pre-load failed."); return; }
            InitializeButton.IsEnabled = false; UpdateStatus("P/INVOKE: Initializing LLM...");
            try { NativeLlmMethods.RegisterTokenCallback_PInvoke(_tokenCallbackInstance); } catch (Exception ex) { UpdateStatus($"P/Invoke Error: {ex.Message}"); SetUiForNoLlm(); return; }
            _modelPathInAppDirectory = (await CopyModelToAppDataAsync()) ?? string.Empty;
            if (string.IsNullOrEmpty(_modelPathInAppDirectory)) { UpdateStatus("Failed to prepare model file."); SetUiForNoLlm(); return; }
            UpdateStatus("Model prepared. Calling initializeLlama...");
            await Task.Run(() =>
            {
                try
                {
                    int nThreads = Math.Max(1, Environment.ProcessorCount / 2); bool success = NativeLlmMethods.InitializeLlama_PInvoke(_modelPathInAppDirectory, 2048, 0, 0.7f, 0.1f, nThreads, nThreads);
                    if (success) { _isLlmInitialized = true; MainThread.BeginInvokeOnMainThread(() => { SetUiForLlmInitialized(); SendInitialPrompt(); }); } else { MainThread.BeginInvokeOnMainThread(() => { UpdateStatus("initializeLlama returned 'false'."); SetUiForNoLlm(); }); }
                }
                catch (Exception ex) { MainThread.BeginInvokeOnMainThread(() => { UpdateStatus($"P/Invoke Exception: {ex.Message}"); SetUiForNoLlm(); }); }
            });
        }
        private void DeinitializeButton_Clicked(object? sender, EventArgs e)
        {
            if (!_isLlmInitialized) return;
            UpdateStatus("De-initializing LLM and clearing state...");
            _generationCts?.Cancel(); _scriptCts?.Cancel(); NativeLlmMethods.StopGeneration_PInvoke(); NativeLlmMethods.FreeLlama_PInvoke();
            _isLlmInitialized = false; _generationTask = null; _scriptCts = null;
            ChatHistoryLayout.Clear(); _chatHistory.Clear();
            SetUiForNoLlm(); UpdateStatus("LLM de-initialized. Ready to load again.");
        }
        #endregion

        #region Chat Interaction & UI Generation
        // --- THIS IS THE KEY FIX ---
        private void SendInitialPrompt()
        {
            // The system prompt defines the AI's persona and rules.
            string systemPrompt = @"You are an expert AI coding assistant for 'Scribby', a 4-wheel robot controlled via Bluetooth LE. For each new request from the user, you must generate a complete, standalone Lua script that achieves the user's goal. Do not provide only changes or additions; always provide the full script. The available functions are:
- send_w() -- Move forward
- send_s() -- Stop all movement
- send_a() -- Turn left
- send_d() -- Turn right
- send_x() -- Move backward
- sleep_ms(milliseconds) -- Pause the script for a duration
- log_info('message') -- Print a message to the app's status log
- should_abort() -- MUST be called inside loops to allow the user to stop the script.
All code you generate must be enclosed in triple backticks, like this: ```-- your lua code here```";

            // 1. Add the rules to the internal history. This is never displayed in the UI.
            _chatHistory.Add(new ChatMessage("system", systemPrompt));

            // 2. Display a friendly welcome message in the UI. This is NOT added to the AI's history.
            string welcomeMessage = "Hello! I am the Scribby AI assistant. I will provide a complete Lua script for every request you make.";
            AddMessageToUI("AI", welcomeMessage);

            // 3. DO NOT start a generation here. Wait for the user's first input.
        }

        private void SendButton_Clicked(object? sender, EventArgs e)
        {
            string? userInput = ChatInputEntry.Text?.Trim();
            if (string.IsNullOrEmpty(userInput)) return;
            ChatInputEntry.Text = "";

            // Add the user's message to both the UI and the internal history
            AddMessageToUI("You", userInput);
            _chatHistory.Add(new ChatMessage("user", userInput));

            // Now, generate a response based on the full history
            StartGeneration();
        }

        private void StartGeneration()
        {
            if (!_isLlmInitialized) return;

            _currentAiResponseBuilder.Clear();
            AddMessageToUI("AI", "...", isStreamingPlaceholder: true);
            UpdateUiForInteractionState(isGenerating: true, isScriptRunning: false);
            UpdateStatus("Generating response...");

            string fullPrompt = BuildFullPrompt();

            _generationCts = new CancellationTokenSource();
            _generationTask = Task.Run(() =>
            {
                try { NativeLlmMethods.GenerateResponse_PInvoke(fullPrompt, 1024); }
                catch (Exception ex) { MainThread.BeginInvokeOnMainThread(() => UpdateStatus($"P/Invoke error: {ex.Message}")); }
                finally
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        ParseAndDisplayFinalResponse(_currentAiResponseBuilder.ToString());
                        UpdateUiForInteractionState(isGenerating: false, isScriptRunning: false);
                        UpdateStatus("Generation finished/stopped.");
                        ScrollToBottom();
                    });
                }
            }, _generationCts.Token);
        }

        private string BuildFullPrompt()
        {
            var promptBuilder = new StringBuilder();
            foreach (var message in _chatHistory)
            {
                // Using a simple format the model can understand
                promptBuilder.AppendLine($"<{message.Role}>");
                promptBuilder.AppendLine(message.Content);
            }
            promptBuilder.AppendLine("<assistant>");
            return promptBuilder.ToString();
        }

        private void ManagedTokenCallback(IntPtr tokenPtr)
        {
            try
            {
                if (tokenPtr == IntPtr.Zero) return;
                string? token = Marshal.PtrToStringUTF8(tokenPtr);
                if (token != null)
                {
                    _currentAiResponseBuilder.Append(token);
                    MainThread.BeginInvokeOnMainThread(() => { if (_streamingResponseLabel != null) { _streamingResponseLabel.Text = _currentAiResponseBuilder.ToString(); ScrollToBottom(); } });
                }
            }
            catch (Exception ex) { Debug.WriteLine($"C# Token Callback Error: {ex.Message}"); }
        }

        private void StopGenerationButton_Clicked(object? sender, EventArgs e)
        {
            if (_generationTask != null && !_generationTask.IsCompleted) { UpdateStatus("Stopping generation..."); NativeLlmMethods.StopGeneration_PInvoke(); _generationCts?.Cancel(); }
        }

        private void AddMessageToUI(string author, string text, bool isStreamingPlaceholder = false)
        {
            var authorLabel = new Label { Text = $"{author}:", FontAttributes = FontAttributes.Bold, TextColor = (author == "You") ? Colors.CornflowerBlue : Colors.MediumPurple };
            var messageContent = new Label { Text = text, LineBreakMode = LineBreakMode.WordWrap };
            ChatHistoryLayout.Children.Add(authorLabel);
            ChatHistoryLayout.Children.Add(messageContent);
            if (isStreamingPlaceholder) _streamingResponseLabel = messageContent;
            ScrollToBottom();
        }

        private void ParseAndDisplayFinalResponse(string fullResponse)
        {
            if (_streamingResponseLabel != null && ChatHistoryLayout.Children.Contains(_streamingResponseLabel))
            {
                int index = ChatHistoryLayout.Children.IndexOf(_streamingResponseLabel);
                if (index > 0) ChatHistoryLayout.Children.RemoveAt(index - 1);
                ChatHistoryLayout.Children.Remove(_streamingResponseLabel);
            }
            _streamingResponseLabel = null;

            // Add the final, complete response to the internal history
            _chatHistory.Add(new ChatMessage("assistant", fullResponse));

            ChatHistoryLayout.Children.Add(new Label { Text = "AI:", FontAttributes = FontAttributes.Bold, TextColor = Colors.MediumPurple });

            var responseToDisplay = string.IsNullOrWhiteSpace(fullResponse) ? "[No response]" : fullResponse.Trim();
            if (!responseToDisplay.Contains("```"))
            {
                ChatHistoryLayout.Children.Add(new Label { Text = responseToDisplay, LineBreakMode = LineBreakMode.WordWrap });
            }
            else
            {
                var parts = responseToDisplay.Split(new[] { "```" }, StringSplitOptions.None);
                for (int i = 0; i < parts.Length; i++)
                {
                    var content = parts[i].Trim(); if (string.IsNullOrEmpty(content)) continue;
                    bool isCode = i % 2 != 0;
                    if (isCode) { string cleanCode = content.StartsWith("lua", StringComparison.OrdinalIgnoreCase) ? content.Substring(3).TrimStart() : content; ChatHistoryLayout.Children.Add(CreateInteractiveCodeBlock(cleanCode)); }
                    else { ChatHistoryLayout.Children.Add(new Label { Text = content, LineBreakMode = LineBreakMode.WordWrap }); }
                }
            }
        }

        private View CreateInteractiveCodeBlock(string code)
        {
            var codeEditor = new Editor { Text = code, Style = (Style)Resources["CodeEditorStyle"], MinimumWidthRequest = 450 };
            var codeScrollView = new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = codeEditor };
            var copyButton = new Button { CommandParameter = code, Style = (Style)Resources["CodeBlockCopyStyle"] };
            copyButton.Clicked += CopyScriptButton_Clicked;
            var runButton = new Button { CommandParameter = code, Style = (Style)Resources["CodeBlockRunStyle"] };
            runButton.Clicked += RunScriptButton_Clicked;
            var buttonContainer = new HorizontalStackLayout { Spacing = 5, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Start, Padding = new Thickness(0, 5, 5, 0) };
            buttonContainer.Children.Add(copyButton);
            buttonContainer.Children.Add(runButton);
            var mainGrid = new Grid();
            mainGrid.Children.Add(codeScrollView); mainGrid.Children.Add(buttonContainer);
            return new Border { Content = mainGrid, Style = (Style)Resources["CodeBlockStyle"] };
        }

        private async void ScrollToBottom() { await Task.Delay(50); await ChatScrollView.ScrollToAsync(ChatHistoryLayout, ScrollToPosition.End, true); }
        #endregion

        #region Lua Scripting & Bluetooth Control
        private void CopyScriptButton_Clicked(object? sender, EventArgs e)
        {
            if (sender is Button { CommandParameter: string scriptToCopy })
            {
                ChatInputEntry.Text = scriptToCopy;
                ChatInputEntry.Focus();
            }
        }
        private void RunScriptButton_Clicked(object? sender, EventArgs e)
        {
            if (sender is not Button runButton || runButton.CommandParameter is not string scriptToRun) return;
            if (!_bluetoothService.IsConnected || _bluetoothService.PrimaryWriteCharacteristic == null) { DisplayAlert("Error", "Not connected. Please use the 'Connect' tab.", "OK"); return; }
            _scriptCts = new CancellationTokenSource();
            UpdateUiForInteractionState(isGenerating: false, isScriptRunning: true);
            LuaLog("Running script...");
            Task.Run(() =>
            {
                try
                {
                    using (Lua lua = new Lua())
                    {
                        lua.State.Encoding = Encoding.UTF8;
                        lua.RegisterFunction("send_w", this, GetType().GetMethod(nameof(SendWCommandForLua)));
                        lua.RegisterFunction("send_a", this, GetType().GetMethod(nameof(SendACommandForLua)));
                        lua.RegisterFunction("send_s", this, GetType().GetMethod(nameof(SendSCommandForLua)));
                        lua.RegisterFunction("send_d", this, GetType().GetMethod(nameof(SendDCommandForLua)));
                        lua.RegisterFunction("send_x", this, GetType().GetMethod(nameof(SendXCommandForLua)));
                        lua.RegisterFunction("sleep_ms", this, GetType().GetMethod(nameof(SleepForLua)));
                        lua.RegisterFunction("log_info", this, GetType().GetMethod(nameof(LuaLog)));
                        lua.RegisterFunction("should_abort", this, GetType().GetMethod(nameof(ShouldAbortScript)));
                        lua.DoString(scriptToRun);
                    }
                    MainThread.BeginInvokeOnMainThread(() => LuaLog("Script finished."));
                }
                catch (OperationCanceledException) { MainThread.BeginInvokeOnMainThread(() => LuaLog("Script aborted by user.")); }
                catch (Exception ex) { MainThread.BeginInvokeOnMainThread(() => LuaLog($"Script error: {ex.Message}")); }
                finally
                {
                    _scriptCts?.Dispose(); _scriptCts = null;
                    MainThread.BeginInvokeOnMainThread(() => UpdateUiForInteractionState(isGenerating: false, isScriptRunning: false));
                }
            });
        }

        private void AbortScriptButton_Clicked(object? sender, EventArgs e)
        {
            LuaLog("Abort requested by user."); _scriptCts?.Cancel();
        }

        private async void SendSButton_Clicked(object? sender, EventArgs e)
        {
            if (_bluetoothService.PrimaryWriteCharacteristic != null)
            {
                LuaLog("Sending manual 'Stop' command...");
                await _bluetoothService.SendCommandAsync(_bluetoothService.PrimaryWriteCharacteristic, "s");
            }
        }

        public void ShouldAbortScript() => _scriptCts?.Token.ThrowIfCancellationRequested();
        public void SendWCommandForLua() => SendCommandForLua("w");
        public void SendACommandForLua() => SendCommandForLua("a");
        public void SendSCommandForLua() => SendCommandForLua("s");
        public void SendDCommandForLua() => SendCommandForLua("d");
        public void SendXCommandForLua() => SendCommandForLua("x");
        private void SendCommandForLua(string command)
        {
            if (_bluetoothService.PrimaryWriteCharacteristic == null) return;
            _bluetoothService.SendCommandAsync(_bluetoothService.PrimaryWriteCharacteristic, command).GetAwaiter().GetResult();
        }
        public void SleepForLua(int milliseconds)
        {
            if (_scriptCts?.Token.WaitHandle.WaitOne(milliseconds) ?? false) _scriptCts?.Token.ThrowIfCancellationRequested();
        }
        public void LuaLog(string message) => UpdateStatus(message);
        #endregion

        private void UpdateStatus(string message) { MainThread.BeginInvokeOnMainThread(() => StatusLabel.Text = $"Status: {message}"); }
    }
}