using ScribbyApp.Services;
using System.Diagnostics;
using System.Web;
using System.IO;

#if ANDROID
using Android.Webkit;
// Using an alias to prevent conflicts with Microsoft.Maui.Controls.WebView
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

        public string Code { set => LoadContentForPlatform(value); }

        public CodePreviewPage(BluetoothService bluetoothService)
        {
            InitializeComponent();
            _bluetoothService = bluetoothService;
            this.Loaded += (s, e) =>
            {
                // We chain the async methods to run after load.
                _ = InitializeWebViewAsync();
            };
        }

        private async Task InitializeWebViewAsync()
        {
            await RequestWebRtcPermissions();
            // This now correctly passes the control to the fixed extension method.
            ConfigureNativeWebView();
        }

        private async void LoadContentForPlatform(string htmlContent)
        {
            string targetDir = Path.Combine(FileSystem.AppDataDirectory, "temp_web_package");

            if (Directory.Exists(targetDir))
            {
                Directory.Delete(targetDir, true);
            }
            Directory.CreateDirectory(targetDir);

            await CopyAssetFolder("www", targetDir);

#if WINDOWS
            // WINDOWS STRATEGY: Requires a full package on disk served via a virtual host.
            try
            {
                await File.WriteAllTextAsync(Path.Combine(targetDir, "index.html"), htmlContent);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WINDOWS ERROR] Failed to write index.html: {ex.Message}");
                await DisplayAlert("Loading Error", "Could not write HTML file for WebView.", "OK");
            }
#elif ANDROID
            // ANDROID STRATEGY: Load HTML as a string but set BaseUrl to the assets folder
            // to fix relative path and CORS issues.
            PreviewWebView.Source = new HtmlWebViewSource
            {
                Html = htmlContent,
                BaseUrl = $"file://{targetDir}/" // The trailing slash is crucial.
            };
#else
            PreviewWebView.Source = new HtmlWebViewSource { Html = htmlContent };
#endif
        }

        private async Task CopyAssetFolder(string sourceFolder, string targetFolder)
        {
            var assetFiles = new[]
            {
                "dist/aframe.js",
                "dist/aframe.min.js",
                "dist/mindar-image-aframe.prod.js",
                "dist/aframe-ar.js",
                "dist/aframe-ar.mjs",
                "dist/aframe-ar-nft.js",
                "dist/aframe-ar-nft.mjs",
                "dist/aframe-ar-location-only.js",
                "dist/aframe-ar-location-only.mjs",
                "dist/aframe-ar-new-location-only.js",
                "dist/aframe-ar-new-location-only.mjs",
                "dist/aframe-extras.controls.js",
                "dist/aframe-extras.controls.min.js",
                "dist/aframe-extras.js",
                "dist/aframe-extras.min.js",
                "dist/aframe-extras.loaders.js",
                "dist/aframe-extras.loaders.min.js",
                "dist/aframe-extras.misc.js",
                "dist/aframe-extras.misc.min.js",
                "dist/aframe-extras.pathfinding.js",
                "dist/aframe-extras.pathfinding.min.js",
                "dist/aframe-extras.primitives.js",
                "dist/aframe-extras.primitives.min.js",
                "dist/mindar-face-aframe.prod.js",
                "dist/mindar-face-three.prod.js",
                "dist/mindar-face.prod.js",
                "dist/mindar-image-three.prod.js",
                "dist/mindar-image.prod.js",
                "dist/controller-BNXQG47f.js",
                "dist/controller-olp5GmPM.js",
                "dist/ui-D7R2QPpe.js",
                "dist/components/grab.js",
                "dist/components/grab.min.js",
                "dist/components/sphere-collider.js",
                "dist/components/sphere-collider.min.js",
                "assets/card-example/card.mind",
                "assets/card-example/card.png",
                "assets/card-example/softmind/scene.gltf",
                "assets/card-example/softmind/scene.bin",
                "assets/card-example/softmind/textures/material_baseColor.png",
                "assets/card-example/softmind/textures/material_metallicRoughness.png",
                "assets/card-example/softmind/textures/material_normal.png",
                "assets/card-example/softmind/textures/material_emissive.png",
            };

            foreach (var relativePath in assetFiles)
            {
                var logicalAssetPath = Path.Combine(sourceFolder, relativePath).Replace('\\', '/');
                var destinationFile = Path.Combine(targetFolder, relativePath);
                var directoryName = Path.GetDirectoryName(destinationFile);
                if (directoryName != null) Directory.CreateDirectory(directoryName);

                try
                {
                    using var stream = await FileSystem.OpenAppPackageFileAsync(logicalAssetPath);
                    using var fileStream = File.Create(destinationFile);
                    await stream.CopyToAsync(fileStream);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Asset Error] Failed to copy '{logicalAssetPath}'. Build Action must be 'MauiAsset'. Error: {ex.Message}");
                }
            }
        }

        private async void ConfigureNativeWebView()
        {
            // Wait for the native control to be initialized.
            await WebViewExtensions.EnsureHandlerIsReady(PreviewWebView);

#if ANDROID
            if (PreviewWebView.Handler?.PlatformView is AWebView platformView)
            {
                platformView.Settings.JavaScriptEnabled = true;
                platformView.Settings.MediaPlaybackRequiresUserGesture = false;
                platformView.Settings.AllowFileAccess = true;
                platformView.SetWebChromeClient(new CustomWebChromeClient());
            }
#elif WINDOWS
            if (PreviewWebView.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.WebView2 platformView)
            {
                await platformView.EnsureCoreWebView2Async();
                platformView.CoreWebView2.PermissionRequested += (sender, args) =>
                {
                    if (args.PermissionKind == CoreWebView2PermissionKind.Camera || args.PermissionKind == CoreWebView2PermissionKind.Microphone)
                    {
                        args.State = CoreWebView2PermissionState.Allow;
                    }
                };
                string contentRoot = Path.Combine(FileSystem.AppDataDirectory, "temp_web_package");
                platformView.CoreWebView2.SetVirtualHostNameToFolderMapping("webapp.local", contentRoot, CoreWebView2HostResourceAccessKind.Allow);
                PreviewWebView.Source = new UrlWebViewSource { Url = "https://webapp.local/index.html" };
            }
#endif
        }

        #region --- Common & Unchanged Methods ---
        protected override void OnAppearing() { base.OnAppearing(); UpdateStatus(); }
        protected override bool OnBackButtonPressed() { if (PreviewWebView.CanGoBack) { PreviewWebView.GoBack(); return true; } return base.OnBackButtonPressed(); }
        private async Task RequestWebRtcPermissions() { var status = await Permissions.RequestAsync<Permissions.Camera>(); if (status != PermissionStatus.Granted) await DisplayAlert("Permission Required", "Camera permission is needed for AR features.", "OK"); }
        private void UpdateStatus() { StatusLabel.Text = _bluetoothService.IsConnected ? "Status: Connected" : "Status: Not connected."; }
        private async void OnWebViewNavigating(object? sender, WebNavigatingEventArgs e)
        {
            if (e.Url != null && e.Url.StartsWith("scribby://"))
            {
                e.Cancel = true;
                if (!_bluetoothService.IsConnected) { StatusLabel.Text = "Error: Not connected."; return; }
                try
                {
                    var uri = new Uri(e.Url);
                    var query = HttpUtility.ParseQueryString(uri.Query);
                    string? command = query["command"];
                    if (!string.IsNullOrEmpty(command) && _validCommands.Contains(command)) await SendCommandInternalAsync(command);
                }
                catch (Exception ex) { Debug.WriteLine($"Error processing command: {ex.Message}"); }
            }
        }
        private async Task SendCommandInternalAsync(string command) { if (_bluetoothService.PrimaryWriteCharacteristic != null) await _bluetoothService.SendCommandAsync(_bluetoothService.PrimaryWriteCharacteristic, command); }
        #endregion
    }

    #region --- Native Platform Helpers ---

#if ANDROID
    internal class CustomWebChromeClient : WebChromeClient
    {
        public override void OnPermissionRequest(PermissionRequest? request) => MainThread.BeginInvokeOnMainThread(() => request?.Grant(request.GetResources()));
    }
#endif

    public static class WebViewExtensions
    {
        // ? FIX: Explicitly specify Microsoft.Maui.Controls.WebView to resolve ambiguity.
        public static Task EnsureHandlerIsReady(this Microsoft.Maui.Controls.WebView webView)
        {
            if (webView.Handler?.PlatformView != null) return Task.CompletedTask;
            var tcs = new TaskCompletionSource<bool>();
            webView.HandlerChanged += OnHandlerChanged;
            return tcs.Task;
            void OnHandlerChanged(object? sender, EventArgs e)
            {
                if (webView.Handler?.PlatformView != null)
                {
                    webView.HandlerChanged -= OnHandlerChanged;
                    tcs.TrySetResult(true);
                }
            }
        }
    }
    #endregion
}