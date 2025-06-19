using Android.App;
using Android.Content.PM;
using Android.OS;
using Android;
using System.Collections.Generic; // Required for List<T>
using System.Linq;

namespace ScribbyApp;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private const int RequestPermissionsCode = 101;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        RequestRequiredPermissions();
    }

    private void RequestRequiredPermissions()
    {
        // We will build a list of permissions we need to request
        var permissionsToRequest = new List<string>();

        // Handle permissions for Android 12 (API 31) and above
        if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
        {
            permissionsToRequest.Add(Manifest.Permission.BluetoothScan);
            permissionsToRequest.Add(Manifest.Permission.BluetoothConnect);
            permissionsToRequest.Add(Manifest.Permission.AccessFineLocation);

            // --- OPTIONAL ---
            // If you absolutely need location for another feature on Android 12+,
            // uncomment the line below. Otherwise, for BLE only, it's not needed.
            // permissionsToRequest.Add(Manifest.Permission.AccessFineLocation);
        }
        // Handle permissions for older versions (Android 6.0 to 11)
        else
        {
            // For these versions, location is MANDATORY for BLE scanning
            permissionsToRequest.Add(Manifest.Permission.AccessFineLocation);
        }

        // Now, filter out the permissions that have already been granted
        var permissionsActuallyNeeded = permissionsToRequest
            .Where(permission => CheckSelfPermission(permission) != Permission.Granted)
            .ToArray();

        // If we have any permissions that need to be requested, request them.
        if (permissionsActuallyNeeded.Any())
        {
            RequestPermissions(permissionsActuallyNeeded, RequestPermissionsCode);
        }
    }
}