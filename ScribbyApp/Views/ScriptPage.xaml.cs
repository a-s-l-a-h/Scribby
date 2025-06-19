using ScribbyApp.Services;
using NLua;
using System.Diagnostics;
using System.Text;

namespace ScribbyApp.Views;

public partial class ScriptPage : ContentPage
{
    private readonly BluetoothService _bluetoothService;
    // A source for our cancellation token to allow aborting the script
    private CancellationTokenSource? _scriptCts;

    public ScriptPage(BluetoothService bluetoothService)
    {
        InitializeComponent();
        _bluetoothService = bluetoothService;
        LoadDefaultScript();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateControlsState();
    }

    private void UpdateControlsState()
    {
        bool isReadyToRun = _bluetoothService.IsConnected && _bluetoothService.PrimaryWriteCharacteristic != null;

        // The Run button is enabled if we are ready and a script is NOT currently running.
        RunLuaScriptButton.IsEnabled = isReadyToRun && (_scriptCts == null);
        CodeEditor.IsEnabled = isReadyToRun && (_scriptCts == null);

        // The Abort button is only enabled if a script IS currently running.
        AbortScriptButton.IsEnabled = isReadyToRun && (_scriptCts != null);
    }

    private void LoadDefaultScript()
    {
        // Added a call to should_abort() inside the loop to make it cancellable
        string defaultScript = @"
log_info('Lua: Script starting...')
for i = 1, 10 do
    -- This call allows the script to be aborted from C#
    should_abort()

    log_info('Lua: Loop ' .. i .. '/10: Sending W')
    send_w()
    sleep_ms(1000)

    -- It's good practice to check for abort after long waits too
    should_abort()
    
    log_info('Lua: Loop ' .. i .. '/10: Sending S')
    send_s()
    sleep_ms(500)
end
log_info('Lua: Script finished successfully.')
".Trim();

        CodeEditor.Text = defaultScript;
        UpdateLineNumbers();
    }

    // This method handles updating the line number display
    private void UpdateLineNumbers()
    {
        var lineCount = CodeEditor.Text.Split('\n').Length;
        var sb = new StringBuilder();
        for (int i = 1; i <= lineCount; i++)
        {
            sb.AppendLine(i.ToString());
        }
        LineNumberEditor.Text = sb.ToString();
    }

    // Event handler for the editor's text changing
    private void OnCodeEditorTextChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateLineNumbers();
    }

    // Click handler for the new Abort button
    private async void OnAbortScriptClicked(object sender, EventArgs e)
    {
        LuaLog("Abort requested by user.");

        // Signal cancellation to the background task
        _scriptCts?.Cancel();

        // Immediately send the 's' (stop) command to the device
        if (_bluetoothService.PrimaryWriteCharacteristic != null)
        {
            await _bluetoothService.SendCommandAsync(_bluetoothService.PrimaryWriteCharacteristic, "s");
        }
    }

    private void OnRunLuaScriptClicked(object sender, EventArgs e)
    {
        if (_bluetoothService.PrimaryWriteCharacteristic == null)
        {
            DisplayAlert("Error", "Not connected. Please use the 'Connect' tab.", "OK");
            return;
        }

        // Create a new cancellation source for this specific run
        _scriptCts = new CancellationTokenSource();

        UpdateControlsState(); // Update UI: Disable Run, Enable Abort
        LuaLog("Lua script starting...");
        string scriptToRun = CodeEditor.Text;

        Task.Run(() =>
        {
            try
            {
                using (Lua lua = new Lua())
                {
                    lua.State.Encoding = Encoding.UTF8;
                    // Register all functions, including our new abort checker
                    lua.RegisterFunction("send_w", this, GetType().GetMethod(nameof(SendWCommandForLua)));
                    lua.RegisterFunction("send_s", this, GetType().GetMethod(nameof(SendSCommandForLua)));
                    lua.RegisterFunction("sleep_ms", this, GetType().GetMethod(nameof(SleepForLua)));
                    lua.RegisterFunction("log_info", this, GetType().GetMethod(nameof(LuaLog)));
                    lua.RegisterFunction("should_abort", this, GetType().GetMethod(nameof(ShouldAbortScript)));

                    lua.DoString(scriptToRun);
                }

                // This part only runs if the script completes without error or cancellation
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    LuaLog("Lua script finished.");
                    await DisplayAlert("Lua Script", "Script finished successfully.", "OK");
                });
            }
            catch (OperationCanceledException)
            {
                // This is the expected exception when the script is aborted. Handle it gracefully.
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    LuaLog("Script execution was successfully aborted.");
                });
            }
            catch (NLua.Exceptions.LuaScriptException luaEx)
            {
                var errorMsg = $"Lua script error: {luaEx.Message}";
                Debug.WriteLine($"NLua Script Error: {luaEx.ToString()}");
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    LuaLog(errorMsg);
                    await DisplayAlert("Lua Error", errorMsg, "OK");
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error running Lua script: {ex.ToString()}");
                var errorMsg = $"System error running script: {ex.Message}";
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    LuaLog(errorMsg);
                    await DisplayAlert("Error", errorMsg, "OK");
                });
            }
            finally
            {
                // This block runs whether the script succeeded, failed, or was aborted.
                // It's the perfect place to clean up and reset the UI.
                _scriptCts?.Dispose();
                _scriptCts = null; // Setting to null indicates no script is running
                MainThread.BeginInvokeOnMainThread(UpdateControlsState);
            }
        });
    }

    #region NLua Functions

    // This function is called from Lua to check for cancellation.
    // It will throw an exception if the Abort button was clicked.
    public void ShouldAbortScript()
    {
        _scriptCts?.Token.ThrowIfCancellationRequested();
    }

    public void SendWCommandForLua()
    {
        if (_bluetoothService.PrimaryWriteCharacteristic == null)
        {
            LuaLog("Lua: Primary write characteristic not ready for W.");
            return;
        }
        _bluetoothService.SendCommandAsync(_bluetoothService.PrimaryWriteCharacteristic, "w").GetAwaiter().GetResult();
    }

    public void SendSCommandForLua()
    {
        if (_bluetoothService.PrimaryWriteCharacteristic == null)
        {
            LuaLog("Lua: Primary write characteristic not ready for S.");
            return;
        }
        _bluetoothService.SendCommandAsync(_bluetoothService.PrimaryWriteCharacteristic, "s").GetAwaiter().GetResult();
    }

    public void SleepForLua(int milliseconds)
    {
        // Instead of a blocking sleep, we do a cancellable wait
        _scriptCts?.Token.WaitHandle.WaitOne(milliseconds);
        _scriptCts?.Token.ThrowIfCancellationRequested();
    }

    public void LuaLog(string message)
    {
        Debug.WriteLine($"LUA: {message}");
        MainThread.BeginInvokeOnMainThread(() => {
            StatusLabel.Text = message;
        });
    }

    #endregion
}