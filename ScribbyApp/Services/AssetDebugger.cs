using System.Diagnostics;
using System.Reflection;

namespace ScribbyApp.Services
{
    public static class AssetDebugger
    {
        public static void LogAllMauiAssets()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceNames = assembly.GetManifestResourceNames();

                Debug.WriteLine("----------[ BEGIN MAUI ASSET LIST ]----------");
                if (resourceNames.Length == 0)
                {
                    Debug.WriteLine("!!!!!! CRITICAL ERROR: NO MAUI ASSETS WERE FOUND IN THE APP !!!!!!");
                }
                else
                {
                    foreach (var name in resourceNames)
                    {
                        // We are only interested in files from our project, which often don't have a namespace prefix
                        // or have the project's default namespace. This helps filter out system/library resources.
                        if (name.StartsWith("ScribbyApp.") || !name.Contains('.'))
                        {
                            Debug.WriteLine($"Found Asset Name: {name}");
                        }
                    }
                }
                Debug.WriteLine("-----------[ END MAUI ASSET LIST ]-----------");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while trying to list assets: {ex.Message}");
            }
        }
    }
}