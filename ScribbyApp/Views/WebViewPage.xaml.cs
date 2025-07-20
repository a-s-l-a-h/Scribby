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

            // --- START: CODE UPDATED AS REQUESTED ---

            // 1. The HTML content from your index.html file is now in a C# string.
            var htmlContent = @"
            <!DOCTYPE html>
            <html>
            <head>
                <title>Scribby Web Control</title>
                <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
                <link rel=stylesheet href=""css/index.css"" />
                <style>
                    body {
                        font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Oxygen, Ubuntu, Cantarell, ""Fira Sans"", ""Droid Sans"", ""Helvetica Neue"", sans-serif;
                        background-color: #f0f0f0;
                        color: #333;
                        text-align: center;
                        margin: 0;
                        padding: 0;
                    }
                    .container {
                        max-width: 320px;
                        margin: 20px auto;
                        padding: 15px;
                        background-color: white;
                        border-radius: 8px;
                        box-shadow: 0 2px 5px rgba(0,0,0,0.1);
                    }
                    h1 {
                        color: #512BD4;
                        margin-top: 0;
                    }
                    .d-pad {
                        display: grid;
                        grid-template-columns: 1fr 1fr 1fr;
                        grid-template-rows: 1fr 1fr 1fr;
                        gap: 10px;
                        width: 240px;
                        height: 240px;
                        margin: 20px auto;
                    }
                    .button {
                        display: flex;
                        justify-content: center;
                        align-items: center;
                        border: none;
                        border-radius: 12px;
                        font-size: 24px;
                        font-weight: bold;
                        color: white;
                        cursor: pointer;
                        user-select: none;
                        -webkit-user-select: none;
                    }
                    .button:active {
                        opacity: 0.7;
                    }
                    .forward { grid-column: 2; grid-row: 1; background-color: cornflowerblue; }
                    .left { grid-column: 1; grid-row: 2; background-color: darkseagreen; }
                    .stop { grid-column: 2; grid-row: 2; background-color: orange; }
                    .right { grid-column: 3; grid-row: 2; background-color: darkseagreen; }
                    .backward { grid-column: 2; grid-row: 3; background-color: indianred; }
                </style>
            </head>
            <body>
                <div class=""container"">
                    <h1>Web Remote</h1>
                    <a href=""https://stemkoski.github.io/AR-Examples/hello-cube.html"">webrtc ar.js demo</a>
                    <p>Use the buttons below to control the robot.</p>
                    <div class=""d-pad"">
                        <div class=""button forward"" onpointerdown=""sendToScribby('w')"" onpointerup=""sendToScribby('s')"">W</div>
                        <div class=""button left"" onpointerdown=""sendToScribby('a')"" onpointerup=""sendToScribby('s')"">A</div>
                        <div class=""button stop"" onclick=""sendToScribby('s')"">S</div>
                        <div class=""button right"" onpointerdown=""sendToScribby('d')"" onpointerup=""sendToScribby('s')"">D</div>
                        <div class=""button backward"" onpointerdown=""sendToScribby('x')"" onpointerup=""sendToScribby('s')"">X</div>
                    </div>
                </div>
                <script>
                    function sendToScribby(command) {
                        window.location.href = `scribby://send?command=${command}`;
                    }
                </script>
                <div class=""fancy"">
                    <a href=""hello-cube.html"">
                        <img src=""images/demo/hello-cube.png"" class=""superImage"" />
                        <br />Basic Cube
                    </a>
                    <p class=""superText"">A basic scene that superimposes a cube on a Hiro marker.</p>
                </div>
                <a href=""./cube.html"">Magic Cube Effect</a>
            </body>
            </html>";

            // 2. A new HtmlWebViewSource is created.
            var htmlSource = new HtmlWebViewSource { Html = htmlContent };

            // IMPORTANT: See the note below about BaseUrl.
            // Without this, relative links to CSS, JS, and other pages will NOT work.
            // htmlSource.BaseUrl = "???"; // This needs to be set correctly.

            // 3. The WebView's source is set to your hardcoded HTML content.
            MyWebView.Source = htmlSource;

            // --- END: CODE UPDATE ---

            this.Loaded += WebViewPage_Loaded;
        }

        // ... THE REST OF YOUR WebViewPage.xaml.cs FILE REMAINS THE SAME ...
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
                platformView.Settings.AllowFileAccess = true;
                platformView.Settings.AllowFileAccessFromFileURLs = true;
                platformView.Settings.AllowUniversalAccessFromFileURLs = true;
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