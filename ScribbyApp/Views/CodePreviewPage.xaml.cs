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
    [QueryProperty(nameof(CodeToPreview), "CodeToPreview")]
    public partial class CodePreviewPage : ContentPage
    {
        private readonly BluetoothService _bluetoothService;
        private readonly HashSet<string> _validCommands = new HashSet<string> { "w", "a", "s", "d", "x" };
        private string _htmlCode;

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

        protected override void OnAppearing()
        {
            base.OnAppearing();
            LoadHtmlFromParameter();
            UpdateStatus();
        }

        private void LoadHtmlFromParameter()
        {
            // Clean the HTML string just in case it has C# verbatim-style quotes ("")
            string correctedHtml = _htmlCode.Replace("\"\"", "\"");

            var htmlSource = new HtmlWebViewSource
            {
                Html = correctedHtml
                // --- CRITICAL FIX: DO NOT SET BaseUrl ---
                // By leaving this out, we allow the MAUI WebView to use its default
                // behavior, which correctly finds assets in the wwwroot folder.
            };

            PreviewWebView.Source = htmlSource;
        }

        /// <summary>
        /// Fired when the page is fully loaded. Used to configure the native WebView.
        /// </summary>
        private async void WebViewPage_Loaded(object? sender, EventArgs e)
        {
            await RequestWebRtcPermissions();
            await ConfigureNativeWebView();
        }

        /// <summary>
        /// Intercepts navigation to handle the custom "scribby://" URL scheme for JS-to-C# communication.
        /// </summary>
        private async void OnWebViewNavigating(object? sender, WebNavigatingEventArgs e)
        {
            if (e.Url == null) return;

            if (e.Url.StartsWith("scribby://"))
            {
                e.Cancel = true; // This is a command, not a web page

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

        /// <summary>
        /// Updates the status label at the bottom of the page based on Bluetooth connection.
        /// </summary>
        private void UpdateStatus()
        {
            StatusLabel.Text = _bluetoothService.IsConnected
                ? $"Status: Connected to {_bluetoothService.GetCurrentlyConnectedDeviceSomehow()?.Name}."
                : "Status: Not connected.";
        }

        /// <summary>
        /// Sends a command string to the connected device via the Bluetooth service.
        /// </summary>
        private async Task SendCommandInternalAsync(string command)
        {
            if (_bluetoothService.PrimaryWriteCharacteristic != null)
            {
                await _bluetoothService.SendCommandAsync(_bluetoothService.PrimaryWriteCharacteristic, command);
            }
        }

        /// <summary>
        /// Prompts the user for Camera and Microphone permissions needed for WebRTC features.
        /// </summary>
        private async Task RequestWebRtcPermissions()
        {
            var cameraStatus = await Permissions.RequestAsync<Permissions.Camera>();
            var microphoneStatus = await Permissions.RequestAsync<Permissions.Microphone>();

            if (cameraStatus != PermissionStatus.Granted || microphoneStatus != PermissionStatus.Granted)
            {
                await DisplayAlert("Permissions Required", "Camera and Microphone permissions are needed for WebRTC features. Please enable them in app settings if you change your mind.", "OK");
            }
        }

        /// <summary>
        /// Applies platform-specific configurations to the underlying native WebView control.
        /// </summary>
        private async Task ConfigureNativeWebView()
        {
            if (PreviewWebView.Handler?.PlatformView == null)
            {
                return;
            }

#if ANDROID
            var platformView = PreviewWebView.Handler.PlatformView as global::Android.Webkit.WebView;
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
            var platformView = PreviewWebView.Handler.PlatformView as WebKit.WKWebView;
            if (platformView != null)
            {
                platformView.Configuration.AllowsInlineMediaPlayback = true;
                platformView.Configuration.MediaTypesRequiringUserActionForPlayback = WebKit.WKAudiovisualMediaTypes.None;
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
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error configuring WebView2: {ex.Message}");
                }
            }
#endif
        }
    }

    /// <summary>
    /// Helper class required on Android to handle runtime permission requests (like camera) from within a WebView.
    /// </summary>
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