using Android.Webkit;

// Namespace should reflect the new folder structure
namespace ScribbyApp.Platforms.Android.Web
{
    /// <summary>
    /// This custom client is required on Android to handle runtime permission requests 
    /// (like for the camera) that originate from within a WebView.
    /// </summary>
    internal class CustomWebChromeClient : WebChromeClient
    {
        // The 'request' parameter is marked as nullable to match the base class signature.
        public override void OnPermissionRequest(PermissionRequest? request)
        {
            if (request == null)
            {
                return;
            }

            // Grant the requested permissions.
            // Note: For a production app, you might want to be more selective here.
            request.Grant(request.GetResources());
        }
    }
}