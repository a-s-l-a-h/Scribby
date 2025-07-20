
using ScribbyApp.Services;
using System.Diagnostics;
using System.Web;

// Add platform-specific using statements here
#if ANDROID
using Android.Webkit;
#elif IOS
using WebKit;
#elif WINDOWS
using Microsoft.Web.WebView2.Core;
#endif

namespace ScribbyApp.Views
{
    public partial class WebViewPage : ContentPage
    {
        private readonly BluetoothService _bluetoothService;
        private readonly HashSet<string> _validCommands = new HashSet<string> { "w", "a", "s", "d", "x" };

        public WebViewPage(BluetoothService bluetoothService)
        {
            InitializeComponent();
            _bluetoothService = bluetoothService;
            MyWebView.Source = new UrlWebViewSource { Url = "index.html" };

            this.Loaded += WebViewPage_Loaded;
        }

        // Make the Loaded event handler async so we can use await inside
        private async void WebViewPage_Loaded(object? sender, EventArgs e)
        {
            await RequestWebRtcPermissions();
            ConfigureNativeWebView();
        }

        protected override bool OnBackButtonPressed()
        {
            if (MyWebView.CanGoBack)
            {
                MyWebView.GoBack();
                return true;
            }
            return base.OnBackButtonPressed();
        }

        private async Task RequestWebRtcPermissions()
        {
            var cameraStatus = await Permissions.RequestAsync<Permissions.Camera>();
            var microphoneStatus = await Permissions.RequestAsync<Permissions.Microphone>();

            if (cameraStatus != PermissionStatus.Granted || microphoneStatus != PermissionStatus.Granted)
            {
                await DisplayAlert("Permissions Required", "Camera and Microphone permissions are needed for WebRTC features. Please enable them in app settings if you change your mind.", "OK");
            }
        }

        private async void ConfigureNativeWebView()
        {
            if (MyWebView.Handler?.PlatformView == null)
            {
                return;
            }

#if ANDROID
            var platformView = MyWebView.Handler.PlatformView as global::Android.Webkit.WebView;
            if (platformView != null)
            {
                platformView.Settings.JavaScriptEnabled = true;
                platformView.Settings.MediaPlaybackRequiresUserGesture = false;

                // --- FIX: Add these lines to allow local file access ---
                platformView.Settings.AllowFileAccess = true;
                platformView.Settings.AllowFileAccessFromFileURLs = true;
                platformView.Settings.AllowUniversalAccessFromFileURLs = true;
                // ---------------------------------------------------------

                platformView.SetWebChromeClient(new CustomWebChromeClient());
            }
#elif IOS
            var platformView = MyWebView.Handler.PlatformView as WKWebView;
            if (platformView != null)
            {
                platformView.Configuration.AllowsInlineMediaPlayback = true;
                platformView.Configuration.MediaTypesRequiringUserActionForPlayback = WKAudiovisualMediaTypes.None;
            }
#elif WINDOWS
            var platformView = MyWebView.Handler.PlatformView as Microsoft.UI.Xaml.Controls.WebView2;
            if (platformView != null)
            {
                // ==========================================================
                // THIS IS THE FIX
                // ==========================================================
                try
                {
                    // WAS: Using .ContinueWith which is not valid on IAsyncAction
                    // NOW: Properly await the initialization of the CoreWebView2.
                    await platformView.EnsureCoreWebView2Async();

                    // Now that it's initialized, we can safely subscribe to the event.
                    platformView.CoreWebView2.PermissionRequested += (sender, args) =>
                    {
                        if (args.PermissionKind == CoreWebView2PermissionKind.Camera ||
                            args.PermissionKind == CoreWebView2PermissionKind.Microphone)
                        {
                            args.State = CoreWebView2PermissionState.Allow;
                        }
                    };
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error configuring WebView2: {ex.Message}");
                }
            }
#endif
        }

        private async void OnWebViewNavigating(object? sender, WebNavigatingEventArgs e)
        {
            if (e.Url == null) return;

            if (e.Url.StartsWith("scribby://"))
            {
                e.Cancel = true;
                if (!_bluetoothService.IsConnected || _bluetoothService.PrimaryWriteCharacteristic == null)
                {
                    StatusLabel.Text = "Error: Cannot send command, not connected.";
                    return;
                }
                try
                {
                    var uri = new Uri(e.Url);
                    var query = HttpUtility.ParseQueryString(uri.Query);
                    string? command = query["command"];

                    if (!string.IsNullOrEmpty(command) && _validCommands.Contains(command))
                    {
                        await SendCommandInternalAsync(command);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error processing WebView command: {ex.Message}");
                }
            }
        }

        // Unchanged methods
        protected override void OnAppearing() { base.OnAppearing(); UpdateStatus(); }
        private void UpdateStatus() { StatusLabel.Text = _bluetoothService.IsConnected ? $"Status: Connected to {_bluetoothService.GetCurrentlyConnectedDeviceSomehow()?.Name}." : "Status: Not connected."; }
        private async Task SendCommandInternalAsync(string command) { if (_bluetoothService.PrimaryWriteCharacteristic != null) await _bluetoothService.SendCommandAsync(_bluetoothService.PrimaryWriteCharacteristic, command); }
    }

#if ANDROID
    internal class CustomWebChromeClient : WebChromeClient
    {
        public override void OnPermissionRequest(PermissionRequest request)
        {
            request.Grant(request.GetResources());
        }
    }
#endif
}