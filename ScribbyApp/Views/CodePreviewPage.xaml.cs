// C:\MYWORLD\Projects\SCRIBBY\ScribbyApp\Views\CodePreviewPage.xaml.cs
using ScribbyApp.Services;
using System.Diagnostics;
using System.Web;
using System.IO;

#if ANDROID
using Android.Webkit;
#elif IOS
using WebKit;
using Foundation;
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

        public string Code { set => PrepareAndLoadWebPackage(value); }

        public CodePreviewPage(BluetoothService bluetoothService)
        {
            InitializeComponent();
            _bluetoothService = bluetoothService;
            this.Loaded += async (s, e) => await RequestWebRtcPermissions();
        }

        private async void PrepareAndLoadWebPackage(string htmlContentFromDb)
        {
            const string sourceAssetFolder = "www";
            string targetDir = Path.Combine(FileSystem.AppDataDirectory, "temp_web_package");

            if (Directory.Exists(targetDir))
            {
                Directory.Delete(targetDir, true);
            }
            Directory.CreateDirectory(targetDir);

            try
            {
                await CopyAssetFolder(sourceAssetFolder, targetDir);
                await File.WriteAllTextAsync(Path.Combine(targetDir, "index.html"), htmlContentFromDb);
                ConfigureNativeWebView(targetDir);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewError] Failed to prepare web package: {ex.Message}");
                await DisplayAlert("Loading Error", $"Could not prepare the web content: {ex.Message}", "OK");
            }
        }

        private async Task CopyAssetFolder(string sourceFolder, string targetFolder)
        {
            var assetFiles = new[]
            {
                // A-Frame and MindAR core libraries
                "dist/aframe.js",
                "dist/aframe.min.js",
                "dist/mindar-image-aframe.prod.js",

                // Other A-Frame variants and plugins
                "dist/aframe-ar.js",
                "dist/aframe-ar.mjs",
                "dist/aframe-ar-nft.js",
                "dist/aframe-ar-nft.mjs",
                "dist/aframe-ar-location-only.js",
                "dist/aframe-ar-location-only.mjs",
                "dist/aframe-ar-new-location-only.js",
                "dist/aframe-ar-new-location-only.mjs",

                // A-Frame extras
                "dist/aframe-extras.controls.js",
                "dist/aframe-extras.controls.min.js",
                "dist/aframe-extras.controls.js.map",
                "dist/aframe-extras.controls.min.js.map",
                "dist/aframe-extras.js",
                "dist/aframe-extras.min.js",
                "dist/aframe-extras.js.map",
                "dist/aframe-extras.min.js.map",
                "dist/aframe-extras.loaders.js",
                "dist/aframe-extras.loaders.min.js",
                "dist/aframe-extras.loaders.js.map",
                "dist/aframe-extras.loaders.min.js.map",
                "dist/aframe-extras.misc.js",
                "dist/aframe-extras.misc.min.js",
                "dist/aframe-extras.misc.js.map",
                "dist/aframe-extras.misc.min.js.map",
                "dist/aframe-extras.pathfinding.js",
                "dist/aframe-extras.pathfinding.min.js",
                "dist/aframe-extras.pathfinding.js.map",
                "dist/aframe-extras.pathfinding.min.js.map",
                "dist/aframe-extras.primitives.js",
                "dist/aframe-extras.primitives.min.js",
                "dist/aframe-extras.primitives.js.map",
                "dist/aframe-extras.primitives.min.js.map",

                // MindAR variants (face/three.js)
                "dist/mindar-face-aframe.prod.js",
                "dist/mindar-face-three.prod.js",
                "dist/mindar-face.prod.js",
                "dist/mindar-image-three.prod.js",
                "dist/mindar-image.prod.js",

                // UI and controller scripts
                "dist/controller-BNXQG47f.js",
                "dist/controller-olp5GmPM.js",
                "dist/ui-D7R2QPpe.js",

                // Custom components
                "dist/components/grab.js",
                "dist/components/grab.min.js",
                "dist/components/grab.js.map",
                "dist/components/grab.min.js.map",
                "dist/components/sphere-collider.js",
                "dist/components/sphere-collider.min.js",
                "dist/components/sphere-collider.js.map",
                "dist/components/sphere-collider.min.js.map",

                // Card example assets
                "assets/card-example/card.mind",
                "assets/card-example/card.png",

                // 3D model files and textures
                "assets/card-example/softmind/scene.gltf",
                "assets/card-example/softmind/scene.bin",
                "assets/card-example/softmind/textures/Material_baseColor.png",
                "assets/card-example/softmind/textures/Material_metallicRoughness.png",
                "assets/card-example/softmind/textures/Material_normal.png",
                "assets/card-example/softmind/textures/Material_emissive.png"
            };

            foreach (var relativePath in assetFiles)
            {
                var logicalAssetPath = Path.Combine(sourceFolder, relativePath).Replace('\\', '/');
                var destinationFile = Path.Combine(targetFolder, relativePath);

                Directory.CreateDirectory(Path.GetDirectoryName(destinationFile));

                Debug.WriteLine($"Attempting to copy MAUI asset: '{logicalAssetPath}'...");
                try
                {
                    using var stream = await FileSystem.OpenAppPackageFileAsync(logicalAssetPath);
                    using var fileStream = File.Create(destinationFile);
                    await stream.CopyToAsync(fileStream);
                    Debug.WriteLine($"Successfully copied to: '{destinationFile}'");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to copy {logicalAssetPath}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// ? FIXED: Enhanced WebView configuration for Windows with better JavaScript support
        /// </summary>
        private async void ConfigureNativeWebView(string contentRoot)
        {
            await PreviewWebView.EnsureCoreWebView2Async_Workaround();

#if ANDROID
            var platformView = PreviewWebView.Handler.PlatformView as global::Android.Webkit.WebView;
            if (platformView != null)
            {
                platformView.Settings.JavaScriptEnabled = true;
                platformView.Settings.MediaPlaybackRequiresUserGesture = false;
                platformView.Settings.AllowFileAccess = true;
                platformView.SetWebChromeClient(new CustomWebChromeClient());
                var indexPath = Path.Combine(contentRoot, "index.html");
                PreviewWebView.Source = new UrlWebViewSource { Url = $"file://{indexPath}" };
            }
#elif IOS
            var platformView = PreviewWebView.Handler.PlatformView as WKWebView;
            if (platformView != null)
            {
                platformView.Configuration.AllowsInlineMediaPlayback = true;
                platformView.Configuration.MediaTypesRequiringUserActionForPlaybook = WKAudiovisualMediaTypes.None;
                
                var fileUrl = new NSUrl(Path.Combine(contentRoot, "index.html"), false);
                var readAccessUrl = new NSUrl(contentRoot, true);
                platformView.LoadFileUrl(fileUrl, readAccessUrl);
            }
#elif WINDOWS
            var platformView = PreviewWebView.Handler.PlatformView as Microsoft.UI.Xaml.Controls.WebView2;
            if (platformView != null)
            {
                await platformView.EnsureCoreWebView2Async();

                // ? CRITICAL: Configure WebView2 settings for better JavaScript support
                var settings = platformView.CoreWebView2.Settings;
                settings.IsScriptEnabled = true; // ? Explicitly enable JavaScript
                settings.AreDefaultScriptDialogsEnabled = true; // ? Allow console.log, alert, etc.
                settings.IsWebMessageEnabled = true; // ? Enable web messaging
                settings.AreDevToolsEnabled = true; // ? Enable dev tools for debugging
                settings.AreHostObjectsAllowed = true; // ? Allow host objects
                settings.IsGeneralAutofillEnabled = false; // ? Disable autofill to avoid interference
                settings.IsPasswordAutosaveEnabled = false; // ? Disable password save

                // ? IMPORTANT: Set user agent to avoid compatibility issues
                settings.UserAgent = settings.UserAgent + " ScribbyApp/1.0";

                // ? Handle JavaScript exceptions and console messages
                platformView.CoreWebView2.ScriptException += (sender, args) =>
                {
                    Debug.WriteLine($"JavaScript Error: {args.Name} - {args.Message}");
                };

                // ? Enable console message logging for debugging
                platformView.CoreWebView2.ConsoleMessage += (sender, args) =>
                {
                    Debug.WriteLine($"Console [{args.Kind}]: {args.Message}");
                };

                // ? Handle permission requests (Camera, Microphone, etc.)
                platformView.CoreWebView2.PermissionRequested += (sender, args) =>
                {
                    Debug.WriteLine($"Permission requested: {args.PermissionKind}");
                    if (args.PermissionKind == CoreWebView2PermissionKind.Camera ||
                        args.PermissionKind == CoreWebView2PermissionKind.Microphone)
                    {
                        args.State = CoreWebView2PermissionState.Allow;
                    }
                };

                // ? CRITICAL: Set up virtual host mapping with proper CORS headers
                platformView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "webapp.local",
                    contentRoot,
                    CoreWebView2HostResourceAccessKind.Allow);

                // ? Add custom headers to prevent CORS issues
                platformView.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
                platformView.CoreWebView2.WebResourceRequested += (sender, args) =>
                {
                    // Add CORS headers
                    args.Response = platformView.CoreWebView2.Environment.CreateWebResourceResponse(
                        null, 200, "OK", "");
                    args.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                    args.Response.Headers.Add("Access-Control-Allow-Methods", "*");
                    args.Response.Headers.Add("Access-Control-Allow-Headers", "*");
                };

                // ? Navigate with error handling
                try
                {
                    Debug.WriteLine($"Loading URL: https://webapp.local/index.html");
                    PreviewWebView.Source = new UrlWebViewSource { Url = "https://webapp.local/index.html" };

                    // ? Add navigation event handlers for debugging
                    platformView.CoreWebView2.NavigationCompleted += (sender, args) =>
                    {
                        Debug.WriteLine($"Navigation completed. Success: {args.IsSuccess}");
                        if (!args.IsSuccess)
                        {
                            Debug.WriteLine($"Navigation failed: {args.WebErrorStatus}");
                        }
                    };

                    platformView.CoreWebView2.DOMContentLoaded += async (sender, args) =>
                    {
                        Debug.WriteLine("DOM Content Loaded");

                        // ? Inject debugging script to verify JavaScript is working
                        await platformView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
                            console.log('? WebView2 JavaScript is enabled and working!');
                            window.addEventListener('load', () => {
                                console.log('? Page load event fired');
                                setTimeout(() => {
                                    console.log('? Delayed script execution working');
                                }, 1000);
                            });
                        ");
                    };
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error setting WebView source: {ex.Message}");
                }
            }
#endif
        }

        #region --- Unchanged Methods ---

        protected override void OnAppearing()
        {
            base.OnAppearing();
            UpdateStatus();
        }

        protected override bool OnBackButtonPressed()
        {
            if (PreviewWebView.CanGoBack)
            {
                PreviewWebView.GoBack();
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
                await DisplayAlert("Permissions Required", "Camera and Microphone permissions are needed for WebRTC features.", "OK");
            }
        }

        private async void OnWebViewNavigating(object? sender, WebNavigatingEventArgs e)
        {
            if (e.Url != null && e.Url.StartsWith("scribby://"))
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

        #endregion
    }

    #region --- Native Platform Helpers ---

#if ANDROID
    internal class CustomWebChromeClient : WebChromeClient
    {
        public override void OnPermissionRequest(PermissionRequest request)
        {
            MainThread.BeginInvokeOnMainThread(() => request.Grant(request.GetResources()));
        }
    }
#endif

    public static class WebViewExtensions
    {
        public static async Task EnsureCoreWebView2Async_Workaround(this WebView webView)
        {
            if (webView.Handler?.PlatformView != null) return;
            var tcs = new TaskCompletionSource<bool>();
            webView.HandlerChanged += (s, e) =>
            {
                if (webView.Handler?.PlatformView != null)
                {
                    tcs.TrySetResult(true);
                }
            };
            await tcs.Task;
        }
    }

    #endregion
}