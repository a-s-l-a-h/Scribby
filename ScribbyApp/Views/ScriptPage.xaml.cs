using ScribbyApp.Services;
using System.Diagnostics;
using System.Text;
using Jint;
using Jint.Runtime;

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
        RunScriptButton.IsEnabled = isReadyToRun && (_scriptCts == null);
        CodeEditor.IsEnabled = isReadyToRun && (_scriptCts == null);

        // The Abort button is only enabled if a script IS currently running.
        AbortScriptButton.IsEnabled = isReadyToRun && (_scriptCts != null);
    }

    private void LoadDefaultScript()
    {
        // MODIFIED: Default script is now cleaner and only uses the two allowed functions.
        // The scribbySleep() function now implicitly handles script abortion.
        string defaultScript = @"
// This script moves the robot forward for 1 second, then stops.
// It repeats this cycle 5 times.
// The script can be aborted at any time by pressing the 'Abort' button.

for (let i = 1; i <= 5; i++) {
    // Move forward
    sendToScribby('w');
    scribbySleep(1000); // Wait 1 second (and check for abort)

    // Stop
    sendToScribby('s');
    scribbySleep(500); // Wait 0.5 seconds (and check for abort)
}

// The script finishes automatically after the loop.
".Trim();

        CodeEditor.Text = defaultScript;
        UpdateLineNumbers();
    }

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

    private void OnCodeEditorTextChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateLineNumbers();
    }

    private async void OnAbortScriptClicked(object sender, EventArgs e)
    {
        LogStatus("Abort requested by user.");
        _scriptCts?.Cancel();
        // Immediately send the 's' (stop) command to the device
        if (_bluetoothService.PrimaryWriteCharacteristic != null)
        {
            await _bluetoothService.SendCommandAsync(_bluetoothService.PrimaryWriteCharacteristic, "s");
        }
    }

    private void OnRunScriptClicked(object sender, EventArgs e)
    {
        if (_bluetoothService.PrimaryWriteCharacteristic == null)
        {
            DisplayAlert("Error", "Not connected. Please use the 'Connect' tab.", "OK");
            return;
        }

        _scriptCts = new CancellationTokenSource();
        UpdateControlsState(); // Update UI: Disable Run, Enable Abort
        LogStatus("JS script starting...");
        string scriptToRun = CodeEditor.Text;

        Task.Run(() =>
        {
            try
            {
                var engine = new Engine(options =>
                {
                    // Enforce cancellation token
                    options.CancellationToken(_scriptCts.Token);
                    // Limit execution time to prevent infinite loops (optional but good practice)
                    options.TimeoutInterval(TimeSpan.FromMinutes(1));
                });

                // MODIFIED: Only expose the two required functions to the JS environment.
                engine.SetValue("sendToScribby", new Action<string>(SendToScribby));
                engine.SetValue("scribbySleep", new Action<int>(ScribbySleep));

                // Execute the script
                engine.Execute(scriptToRun);

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    LogStatus("JS script finished successfully.");
                });
            }
            catch (OperationCanceledException)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    LogStatus("Script execution was successfully aborted.");
                });
            }
            catch (JavaScriptException jsEx)
            {
                var errorMsg = $"JS script error: {jsEx.Message}";
                Debug.WriteLine($"Jint Script Error: {jsEx.ToString()}");
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    LogStatus(errorMsg);
                    DisplayAlert("JavaScript Error", errorMsg, "OK");
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error running JS script: {ex.ToString()}");
                var errorMsg = $"System error running script: {ex.Message}";
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    LogStatus(errorMsg);
                    DisplayAlert("Error", errorMsg, "OK");
                });
            }
            finally
            {
                _scriptCts?.Dispose();
                _scriptCts = null;
                MainThread.BeginInvokeOnMainThread(UpdateControlsState);
            }
        });
    }

    #region C# Functions for Jint

    // Exposed to JS as sendToScribby('...')
    public void SendToScribby(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return;

        if (_bluetoothService.PrimaryWriteCharacteristic == null)
        {
            LogStatus("Error: Bluetooth characteristic not ready.");
            return;
        }
        // Use GetAwaiter().GetResult() to run the async method synchronously
        // from the non-async script context. This is safe because it's on a background thread.
        _bluetoothService.SendCommandAsync(_bluetoothService.PrimaryWriteCharacteristic, command.Trim()).GetAwaiter().GetResult();
    }

    // Exposed to JS as scribbySleep(...)
    // MODIFIED: This function now ALSO handles checking for abort requests.
    public void ScribbySleep(int milliseconds)
    {
        // Instead of a blocking sleep, we do a cancellable wait.
        // If the token is cancelled, WaitOne returns true and we throw.
        if (_scriptCts?.Token.WaitHandle.WaitOne(milliseconds) ?? false)
        {
            _scriptCts?.Token.ThrowIfCancellationRequested();
        }
    }

    // This is now a private helper for C# to update the UI, not exposed to JS.
    private void LogStatus(string message)
    {
        Debug.WriteLine($"ScriptPage: {message}");
        MainThread.BeginInvokeOnMainThread(() => {
            StatusLabel.Text = message;
        });
    }

    #endregion
}