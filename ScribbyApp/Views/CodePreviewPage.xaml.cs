using ScribbyApp.Services;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Web;

// Platform-specific using statements
#if ANDROID
using Android.Webkit;
#elif IOS
using WebKit;
#elif WINDOWS
using Microsoft.Web.WebView2.Core;
#endif

namespace ScribbyApp.Views;

[QueryProperty(nameof(Code), "Code")]
public partial class CodePreviewPage : ContentPage
{
    private readonly BluetoothService _bluetoothService;
    private readonly HashSet<string> _validCommands = new() { "w", "a", "s", "d", "x" };

    public string Code { set => LoadCode(value); }

    public CodePreviewPage(BluetoothService bluetoothService)
    {
        InitializeComponent();
        _bluetoothService = bluetoothService;
        this.Loaded += CodePreviewPage_Loaded;
    }

    private async void LoadCode(string htmlContent)
    {
        try
        {
#if WINDOWS
            // On Windows, WebView2 has strict security policies. We must copy all assets
            // to a local, writable folder and then load from a file:// URI.
            await LoadCodeForWindowsLocalAsync(htmlContent);
#else
            // For Android and iOS, setting a secure BaseUrl is sufficient.
            PreviewWebView.Source = new HtmlWebViewSource
            {
                Html = htmlContent,
                BaseUrl = "https://scribby.app.local/"
            };
#endif
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FATAL] Error in LoadCode: {ex.Message}");
            await DisplayAlert("Load Error", "An error occurred while preparing the script for preview.", "OK");
        }
    }

#if WINDOWS
    /// <summary>
    /// Implements the required workflow for local files on Windows.
    /// </summary>
    private async Task LoadCodeForWindowsLocalAsync(string htmlBodyContent)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appName = Assembly.GetExecutingAssembly().GetName().Name;
        var webContentPath = Path.Combine(localAppData, appName, "WebContent");

        Directory.CreateDirectory(webContentPath);

        // This list contains all the files the HTML needs. It must match your wwwroot structure.
        var requiredAssets = new List<string>
        {
            "dist/aframe.min.js",
            "dist/mindar-image-aframe.prod.js",
            "assets/card-example/card.mind",
            "assets/card-example/card.png",
            "assets/card-example/softmind/scene.gltf",
            "assets/card-example/softmind/scene.bin" // Required by scene.gltf
            // If your model has textures, add them here too.
            // e.g., "assets/card-example/softmind/textures/texture.png"
        };

        foreach (var assetPath in requiredAssets)
        {
            await CopyAssetToWritableLocation(assetPath, Path.Combine(webContentPath, assetPath));
        }

        // Construct the final HTML, adding the necessary script tags.
        string finalHtml = $@"
            <!DOCTYPE html>
            <html>
            <head>
                <meta name='viewport' content='width=device-width, initial-scale=1' />
                <meta charset='utf-8'>
                <script src='./dist/aframe.min.js'></script>
                <script src='./dist/mindar-image-aframe.prod.js'></script>
            </head>
            <body>
            {htmlBodyContent}
            </body>
            </html>";

        var htmlPath = Path.Combine(webContentPath, "index.html");
        await File.WriteAllTextAsync(htmlPath, finalHtml);

        var fileUrl = new Uri(htmlPath).AbsoluteUri;
        PreviewWebView.Source = new UrlWebViewSource { Url = fileUrl };
        Debug.WriteLine($"[WINDOWS] Loading content from local file: {fileUrl}");
    }

    /// <summary>
    /// Helper method to copy a file from the app package to a writable location.
    /// </summary>
    private async Task CopyAssetToWritableLocation(string assetPath, string targetPath)
    {
        try
        {
            if (File.Exists(targetPath)) return;
            var targetDirectory = Path.GetDirectoryName(targetPath);
            if (targetDirectory != null) Directory.CreateDirectory(targetDirectory);
            using var sourceStream = await FileSystem.Current.OpenAppPackageFileAsync(assetPath);
            using var targetStream = File.Create(targetPath);
            await sourceStream.CopyToAsync(targetStream);
        }
        catch (FileNotFoundException)
        {
            Debug.WriteLine($"[ERROR] App package asset not found: {assetPath}. Check spelling and ensure Build Action is MauiAsset.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] Failed to copy asset {assetPath}: {ex.Message}");
        }
    }
#endif

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

    private async void ConfigureNativeWebView()
    {
        await Task.Delay(100); // Wait for handler to initialize
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
                platformView.CoreWebView2.PermissionRequested += CoreWebView2_PermissionRequested;
            }
            catch (Exception ex) { Debug.WriteLine($"Error configuring WebView2: {ex.Message}"); }
        }
#endif
    }

#if WINDOWS
    private void CoreWebView2_PermissionRequested(object sender, CoreWebView2PermissionRequestedEventArgs args)
    {
        if (args.PermissionKind is CoreWebView2PermissionKind.Camera or CoreWebView2PermissionKind.Microphone)
        {
            args.State = CoreWebView2PermissionState.Allow;
        }
    }
#endif

    private async void OnWebViewNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (e.Url == null || !e.Url.StartsWith("scribby://")) return;

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
// Custom WebChromeClient to auto-grant permissions on Android
internal class CustomWebChromeClient : WebChromeClient
{
    public override void OnPermissionRequest(PermissionRequest request)
    {
        request.Grant(request.GetResources());
    }
}
#endif