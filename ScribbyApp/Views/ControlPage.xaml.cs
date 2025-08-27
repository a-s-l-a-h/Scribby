using ScribbyApp.Services;
using System.Diagnostics;

// Platform-specific using statements
#if WINDOWS
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;
#endif

namespace ScribbyApp.Views;

public partial class ControlPage : ContentPage
{
    private readonly BluetoothService _bluetoothService;
    private bool _isKeyDown = false;

    public ControlPage(BluetoothService bluetoothService)
    {
        InitializeComponent();
        _bluetoothService = bluetoothService;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _bluetoothService.StatusChanged += OnStatusChanged;
        UpdateControlsState();
        HookKeyboardEvents();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _bluetoothService.StatusChanged -= OnStatusChanged;
        UnhookKeyboardEvents();
    }

    // --- CORRECTED AND FINAL: PLATFORM-SPECIFIC KEYBOARD HANDLING ---

    private void HookKeyboardEvents()
    {
#if WINDOWS
        // Get the main application window. This is the key to global keyboard listening.
        var nativeWindow = this.GetParentWindow()?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
        if (nativeWindow != null)
        {
            // The Content of the window is the root UI element that can receive key events.
            nativeWindow.Content.KeyDown += Page_KeyDown;
            nativeWindow.Content.KeyUp += Page_KeyUp;
        }
#elif MACCATALYST
        // MacCatalyst handling would be different
#endif
    }

    private void UnhookKeyboardEvents()
    {
#if WINDOWS
        var nativeWindow = this.GetParentWindow()?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
        if (nativeWindow != null)
        {
            nativeWindow.Content.KeyDown -= Page_KeyDown;
            nativeWindow.Content.KeyUp -= Page_KeyUp;
        }
#endif
    }

#if WINDOWS
    private void Page_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_isKeyDown) return;

        string? command = null;
        switch (e.Key)
        {
            case VirtualKey.W:
            case VirtualKey.Up:
                command = "w";
                break;
            case VirtualKey.A:
            case VirtualKey.Left:
                command = "a";
                break;
            case VirtualKey.D:
            case VirtualKey.Right:
                command = "d";
                break;
            case VirtualKey.X:
            case VirtualKey.Down:
                command = "x";
                break;
            case VirtualKey.S:
                command = "s";
                break;
        }

        if (command != null)
        {
            _isKeyDown = true;
            _ = SendCommandInternalAsync(command, $"Key ({e.Key})");
        }
    }

    private void Page_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        _isKeyDown = false;
        
        switch (e.Key)
        {
            case VirtualKey.W:
            case VirtualKey.Up:
            case VirtualKey.A:
            case VirtualKey.Left:
            case VirtualKey.D:
            case VirtualKey.Right:
            case VirtualKey.X:
            case VirtualKey.Down:
                _ = SendCommandInternalAsync("s", "Stop (Key Release)");
                break;
        }
    }
#endif

    

    private void UpdateControlsState()
    {
        bool isReady = _bluetoothService.IsConnected && _bluetoothService.PrimaryWriteCharacteristic != null;

        ForwardButton.IsEnabled = isReady;
        LeftButton.IsEnabled = isReady;
        StopButton.IsEnabled = isReady;
        RightButton.IsEnabled = isReady;
        BackButton.IsEnabled = isReady;

        if (isReady)
        {
            StatusLabel.Text = $"Ready! Device: {_bluetoothService.GetCurrentlyConnectedDeviceSomehow()?.Name}";
        }
        else
        {
            StatusLabel.Text = "Please connect to a device from the 'Connect' tab first.";
        }
    }

    private void OnStatusChanged(object? sender, string status)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (status.ToLower().Contains("disconnected"))
            {
                UpdateControlsState();
            }
            else
            {
                StatusLabel.Text = status;
            }
        });
    }

    private async void OnDirectionalButtonPressed(object? sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is string command)
        {
            await SendCommandInternalAsync(command, $"Command ({command.ToUpper()})");
        }
    }

    private async void OnDirectionalButtonReleased(object? sender, EventArgs e)
    {
        await SendCommandInternalAsync("s", "Stop (Button Release)");
    }

    private async void OnStopButtonClicked(object sender, EventArgs e)
    {
        await SendCommandInternalAsync("s", "Stop (S)");
    }

    private async Task SendCommandInternalAsync(string command, string commandFriendlyName)
    {
        var characteristic = _bluetoothService.PrimaryWriteCharacteristic;
        if (characteristic == null)
        {
            MainThread.BeginInvokeOnMainThread(() => StatusLabel.Text = "Error: Not connected.");
            return;
        }

        MainThread.BeginInvokeOnMainThread(() => StatusLabel.Text = $"Sending: {commandFriendlyName}");
        try
        {
            await _bluetoothService.SendCommandAsync(characteristic, command);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error sending command: {ex.Message}");
            MainThread.BeginInvokeOnMainThread(() => StatusLabel.Text = $"Send Error: {ex.Message.Split('\n')[0]}");
        }
    }
}