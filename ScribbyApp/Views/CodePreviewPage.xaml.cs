// C:\MYWORLD\Projects\SCRIBBY\ScribbyApp\Views\CodePreviewPage.xaml.cs
using ScribbyApp.Services;
using System.Diagnostics;
using System.Web;
using System.IO;

#if ANDROID
using Android.Webkit;
using AWebView = Android.Webkit.WebView;
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
            // The name of our web content folder inside Resources/Raw.
            // This MUST match the folder name in your project structure.
            const string sourceAssetFolder = "www";

            // A temporary directory in the app's local data storage to build our web package.
            string targetDir = Path.Combine(FileSystem.AppDataDirectory, "temp_web_package");

            // Clean up any previous run to ensure fresh files.
            if (Directory.Exists(targetDir))
            {
                Directory.Delete(targetDir, true);
            }
            Directory.CreateDirectory(targetDir);

            try
            {
                // Step 1: Copy supporting files (dist, assets) from the bundled assets.
                await CopyAssetFolder(sourceAssetFolder, targetDir);

                // Step 2: Write the dynamic HTML from the database into the package.
                // This makes the relative paths like "./dist/script.js" work.
                await File.WriteAllTextAsync(Path.Combine(targetDir, "index.html"), htmlContentFromDb);

                // Step 3: Configure the WebView for the specific platform and load our new content.
                ConfigureNativeWebView(targetDir);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewError] Failed to prepare web package: {ex.Message}");
                await DisplayAlert("Loading Error", $"Could not prepare the web content: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// Copies bundled assets from Resources/Raw/{sourceFolder} to a physical directory at runtime.
        /// </summary>
        private async Task CopyAssetFolder(string sourceFolder, string targetFolder)
        {
            // List all files needed by your HTML, relative to the sourceFolder.
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
                "assets/card-example/softmind/textures/material_baseColor.png",
                "assets/card-example/softmind/textures/material_metallicRoughness.png",
                "assets/card-example/softmind/textures/material_normal.png",
                "assets/card-example/softmind/textures/material_emissive.png"
            };

            foreach (var relativePath in assetFiles)
            {
                // The logical path for OpenAppPackageFileAsync is relative to the project's root Raw folder.
                var logicalAssetPath = Path.Combine(sourceFolder, relativePath).Replace('\\', '/');
                var destinationFile = Path.Combine(targetFolder, relativePath);

                // Ensure the target subdirectory (e.g., 'dist', 'assets/card-example') exists.
                var directoryName = Path.GetDirectoryName(destinationFile);
                if (directoryName != null)
                {
                    Directory.CreateDirectory(directoryName);
                }

                Debug.WriteLine($"Attempting to copy MAUI asset: '{logicalAssetPath}'...");
                using var stream = await FileSystem.OpenAppPackageFileAsync(logicalAssetPath);
                using var fileStream = File.Create(destinationFile);
                await stream.CopyToAsync(fileStream);
                Debug.WriteLine($"Successfully copied to: '{destinationFile}'");
            }
        }

        /// <summary>
        /// Configures the native WebView control on each platform to load the content correctly.
        /// </summary>
        private async void ConfigureNativeWebView(string contentRoot)
        {
            // This helper waits until the WebView's native control is initialized to prevent errors.
            await WebViewExtensions.EnsureCoreWebView2Async_Workaround(PreviewWebView);

#if ANDROID
            var platformView = PreviewWebView.Handler?.PlatformView as AWebView;
            if (platformView != null)
            {
                platformView.Settings.JavaScriptEnabled = true;
                platformView.Settings.MediaPlaybackRequiresUserGesture = false;
                platformView.Settings.AllowFileAccess = true; // CRITICAL for local file access
                platformView.SetWebChromeClient(new CustomWebChromeClient()); // CRITICAL for camera permissions
                var indexPath = Path.Combine(contentRoot, "index.html");
                PreviewWebView.Source = new UrlWebViewSource { Url = $"file://{indexPath}" };
            }
#elif IOS
            var platformView = PreviewWebView.Handler?.PlatformView as WKWebView;
            if (platformView != null)
            {
                platformView.Configuration.AllowsInlineMediaPlayback = true;
                platformView.Configuration.MediaTypesRequiringUserActionForPlayback = WKAudiovisualMediaTypes.None;
                
                // For iOS, you must explicitly grant read access to the directory containing the file.
                var fileUrl = new NSUrl(Path.Combine(contentRoot, "index.html"), false);
                var readAccessUrl = new NSUrl(contentRoot, true);
                platformView.LoadFileUrl(fileUrl, readAccessUrl);
            }
#elif WINDOWS
            var platformView = PreviewWebView.Handler?.PlatformView as Microsoft.UI.Xaml.Controls.WebView2;
            if (platformView != null)
            {
                await platformView.EnsureCoreWebView2Async();

                // This event handler grants camera/microphone permissions when the web content requests them.
                platformView.CoreWebView2.PermissionRequested += (sender, args) =>
                {
                    if (args.PermissionKind == CoreWebView2PermissionKind.Camera || args.PermissionKind == CoreWebView2PermissionKind.Microphone)
                    {
                        args.State = CoreWebView2PermissionState.Allow;
                    }
                };

                // This creates a secure https:// origin, which is REQUIRED for WebRTC on Windows.
                platformView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "webapp.local", // A safe, virtual domain name
                    contentRoot,    // The physical folder it points to
                    CoreWebView2HostResourceAccessKind.Allow);

                PreviewWebView.Source = new UrlWebViewSource { Url = "https://webapp.local/index.html" };
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
        // This is required on Android to intercept and grant permission requests from the web content.
        public override void OnPermissionRequest(PermissionRequest? request)
        {
            if (request != null)
            {
                MainThread.BeginInvokeOnMainThread(() => request.Grant(request.GetResources()));
            }
        }
    }
#endif

    public static class WebViewExtensions
    {
        // This helper extension method avoids race conditions during WebView initialization.
        public static async Task EnsureCoreWebView2Async_Workaround(this Microsoft.Maui.Controls.WebView webView)
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