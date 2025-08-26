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
        var permissionsToRequest = new List<string>();

        // Handle permissions for Android 12 (API 31) and above
        if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
        {
            permissionsToRequest.Add(Manifest.Permission.BluetoothScan);
            permissionsToRequest.Add(Manifest.Permission.BluetoothConnect);
        }
        // Handle permissions for older versions
        else
        {
            permissionsToRequest.Add(Manifest.Permission.AccessFineLocation);
        }

        var permissionsActuallyNeeded = permissionsToRequest
            .Where(permission => CheckSelfPermission(permission) != Permission.Granted)
            .ToArray();

        if (permissionsActuallyNeeded.Any())
        {
            RequestPermissions(permissionsActuallyNeeded, RequestPermissionsCode);
        }
    }
}