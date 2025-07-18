// C:\MYWORLD\Projects\SCRIBBY\ScribbyApp\Views\CodePreviewPage.xaml.cs
using ScribbyApp.Services;
using System.Diagnostics;
using System.Web;

#if ANDROID
using Android.Webkit;
#elif IOS
using WebKit;
#elif WINDOWS
using Microsoft.Web.WebView2.Core;
#endif

namespace ScribbyApp.Views
{
    [QueryProperty(nameof(Code), "Code")]
    public partial class CodePreviewPage : ContentPage
    {
        private readonly BluetoothService _bluetoothService;
        private readonly HashSet<string> _validCommands = new HashSet<string> { "w", "a", "s", "d", "x" };

        public string Code { set => LoadCode(value); }

        public CodePreviewPage(BluetoothService bluetoothService)
        {
            InitializeComponent();
            _bluetoothService = bluetoothService;
            this.Loaded += CodePreviewPage_Loaded;
        }

        private void LoadCode(string htmlContent)
        {
            PreviewWebView.Source = new HtmlWebViewSource { Html = htmlContent };
        }

        private async void CodePreviewPage_Loaded(object? sender, EventArgs e)
        {
            await RequestWebRtcPermissions();
            ConfigureNativeWebView();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            UpdateStatus();
        }

        // --- UPDATED: This is the perfected back button logic ---
        protected override bool OnBackButtonPressed()
        {
            // First, check if the WebView can navigate back internally
            if (PreviewWebView.CanGoBack)
            {
                // If it can, tell the WebView to go back
                PreviewWebView.GoBack();
                // Return true to signify that we have handled the back button press
                // and the app should NOT close the page.
                return true;
            }
            else
            {
                // If the WebView cannot go back, we want to close the page itself.
                // We do this by calling the base implementation.
                return base.OnBackButtonPressed();
            }
        }

        private async Task RequestWebRtcPermissions()
        {
            var cameraStatus = await Permissions.RequestAsync<Permissions.Camera>();
            var microphoneStatus = await Permissions.RequestAsync<Permissions.Microphone>();
            if (cameraStatus != PermissionStatus.Granted || microphoneStatus != PermissionStatus.Granted)
            {
                await DisplayAlert("Permissions Required", "Camera and Microphone permissions are needed for WebRTC features.", "OK");
            }
        }

        private async void ConfigureNativeWebView()
        {
            if (PreviewWebView.Handler?.PlatformView == null) return;
#if ANDROID
            var platformView = PreviewWebView.Handler.PlatformView as global::Android.Webkit.WebView;
            if (platformView != null)
            {
                platformView.Settings.JavaScriptEnabled = true;
                platformView.Settings.MediaPlaybackRequiresUserGesture = false;
                platformView.SetWebChromeClient(new CustomWebChromeClient());
            }
#elif IOS
            var platformView = PreviewWebView.Handler.PlatformView as WKWebView;
            if (platformView != null)
            {
                platformView.Configuration.AllowsInlineMediaPlayback = true;
                platformView.Configuration.MediaTypesRequiringUserActionForPlayback = WKAudiovisualMediaTypes.None;
            }
#elif WINDOWS
            var platformView = PreviewWebView.Handler.PlatformView as Microsoft.UI.Xaml.Controls.WebView2;
            if (platformView != null)
            {
                try
                {
                    await platformView.EnsureCoreWebView2Async();
                    platformView.CoreWebView2.PermissionRequested += (sender, args) =>
                    {
                        if (args.PermissionKind == CoreWebView2PermissionKind.Camera ||
                            args.PermissionKind == CoreWebView2PermissionKind.Microphone)
                        {
                            args.State = CoreWebView2PermissionState.Allow;
                        }
                    };
                }
                catch (Exception ex) { Debug.WriteLine($"Error configuring WebView2: {ex.Message}"); }
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
                catch (Exception ex) { Debug.WriteLine($"Error processing WebView command: {ex.Message}"); }
            }
        }

        private void UpdateStatus()
        {
            StatusLabel.Text = _bluetoothService.IsConnected ? $"Status: Connected to {_bluetoothService.GetCurrentlyConnectedDeviceSomehow()?.Name}." : "Status: Not connected.";
        }

        private async Task SendCommandInternalAsync(string command)
        {
            if (_bluetoothService.PrimaryWriteCharacteristic != null)
            {
                await _bluetoothService.SendCommandAsync(_bluetoothService.PrimaryWriteCharacteristic, command);
            }
        }
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