using ScribbyApp.Services;
using System.Diagnostics;
using System.Web;

// Add platform-specific using statements here
#if ANDROID
using Android.Webkit;
#elif IOS
using WebKit;
#endif

namespace ScribbyApp.Views
{
    [QueryProperty(nameof(CodeToPreview), "CodeToPreview")]
    public partial class CodePreviewPage : ContentPage
    {
        private readonly BluetoothService _bluetoothService;
        private string _htmlCode;
        private bool _isPausedByButton = false;
        private const int MAX_COMMAND_LENGTH = 10;

        public string CodeToPreview
        {
            set => _htmlCode = Uri.UnescapeDataString(value ?? string.Empty);
        }

        public CodePreviewPage(BluetoothService bluetoothService)
        {
            InitializeComponent();
            _bluetoothService = bluetoothService;
            this.Loaded += WebViewPage_Loaded;
        }

        // --- NEW METHOD ---
        // This method intercepts the system back button press.
        protected override bool OnBackButtonPressed()
        {
            // Check if the WebView can navigate backwards in its history
            if (PreviewWebView.CanGoBack)
            {
                // If it can, perform the back navigation within the WebView
                PreviewWebView.GoBack();
                // Return true to indicate we've handled the event and to
                // prevent the app from navigating back a page.
                return true;
            }
            else
            {
                // If the WebView cannot go back, return false to allow the
                // default system behavior (which is to navigate back a page).
                return base.OnBackButtonPressed();
            }
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            LoadHtmlFromParameter();
            UpdateStatus();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            if (_bluetoothService.IsConnected)
            {
                Task.Run(() => SendCommandInternalAsync("s"));
            }
        }

        private void LoadHtmlFromParameter()
        {
            string correctedHtml = _htmlCode.Replace("\"\"", "\"");
            var htmlSource = new HtmlWebViewSource { Html = correctedHtml };
            PreviewWebView.Source = htmlSource;
        }

        private async void WebViewPage_Loaded(object? sender, EventArgs e)
        {
            await ConfigureNativeWebView();
        }

        private async void OnEmergencyStopClicked(object sender, EventArgs e)
        {
            _isPausedByButton = !_isPausedByButton; // Toggle the state

            if (_isPausedByButton)
            {
                EmergencyStopButton.Text = "Resume";
                EmergencyStopButton.BackgroundColor = Colors.Green;
                await SendCommandInternalAsync("s");
                StatusLabel.Text = "Status: Paused by user. Press 'Resume' to continue.";
            }
            else
            {
                EmergencyStopButton.Text = "Stop";
                EmergencyStopButton.BackgroundColor = Colors.Red;
                UpdateStatus();
            }
        }

        private async void OnWebViewNavigating(object? sender, WebNavigatingEventArgs e)
        {
            if (e.Url == null || !e.Url.StartsWith("scribby://")) return;

            e.Cancel = true;

            if (_isPausedByButton) return;

            if (!_bluetoothService.IsConnected || _bluetoothService.PrimaryWriteCharacteristic == null)
            {
                StatusLabel.Text = "Error: Cannot send command, not connected.";
                return;
            }

            try
            {
                var uri = new Uri(e.Url);
                var query = HttpUtility.ParseQueryString(uri.Query);
                string? command = query["command"]?.Trim();

                if (string.IsNullOrEmpty(command)) return;

                if (command.Length > MAX_COMMAND_LENGTH)
                {
                    Debug.WriteLine($"Rejected command (too long): {command}");
                    StatusLabel.Text = $"Status: Command too long (max {MAX_COMMAND_LENGTH} chars).";
                    return;
                }

                bool isPrimitive = command.Length == 1 && "wasdx".Contains(command);
                bool isAdvanced = command.Contains("-");

                if (isPrimitive || isAdvanced)
                {
                    await SendCommandInternalAsync(command);
                }
                else
                {
                    Debug.WriteLine($"Invalid command format from WebView: {command}");
                    StatusLabel.Text = $"Status: Invalid command '{command}'";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing WebView command: {ex.Message}");
                StatusLabel.Text = "Status: Error processing command.";
            }
        }

        private void UpdateStatus()
        {
            if (_isPausedByButton) return;

            StatusLabel.Text = _bluetoothService.IsConnected
                ? $"Status: Connected to {_bluetoothService.GetCurrentlyConnectedDeviceSomehow()?.Name}."
                : "Status: Not connected.";
        }

        private async Task SendCommandInternalAsync(string command)
        {
            if (_bluetoothService.PrimaryWriteCharacteristic != null)
            {
                try
                {
                    await _bluetoothService.SendCommandAsync(_bluetoothService.PrimaryWriteCharacteristic, command);

                    if (!_isPausedByButton)
                    {
                        MainThread.BeginInvokeOnMainThread(() => StatusLabel.Text = $"Status: Sent '{command}'");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error sending command: {ex.Message}");
                    MainThread.BeginInvokeOnMainThread(() => StatusLabel.Text = $"Status: Send Error!");
                }
            }
        }

        private async Task ConfigureNativeWebView()
        {
            if (PreviewWebView.Handler?.PlatformView == null) return;

#if ANDROID
            var platformView = PreviewWebView.Handler.PlatformView as global::Android.Webkit.WebView;
            if (platformView != null)
            {
                platformView.Settings.JavaScriptEnabled = true;
                platformView.Settings.AllowFileAccess = true;
                platformView.Settings.AllowFileAccessFromFileURLs = true;
                //platformView.Settings.AllowUniversalAccessFromFileURLs = true;
            }
#elif IOS
            var platformView = PreviewWebView.Handler.PlatformView as WebKit.WKWebView;
#elif WINDOWS
            var platformView = PreviewWebView.Handler.PlatformView as Microsoft.UI.Xaml.Controls.WebView2;
            if (platformView != null)
            {
                try
                {
                    await platformView.EnsureCoreWebView2Async();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error configuring WebView2: {ex.Message}");
                }
            }
#endif
        }
    }
}