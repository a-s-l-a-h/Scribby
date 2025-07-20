using System.Diagnostics;

namespace ScribbyApp.Services
{
    public class AssetService
    {
        private static bool _isCopying = false;
        private static readonly object _lock = new object();

        /// <summary>
        /// Copies all files from the app's wwwroot package into a public cache directory
        /// by reading a manifest file (filelist.txt) that lists all assets.
        /// </summary>
        public async Task CopyWwwRootToCacheIfNeededAsync()
        {
            lock (_lock)
            {
                if (_isCopying) return;
                _isCopying = true;
            }

            try
            {
                string targetDir = Path.Combine(FileSystem.CacheDirectory, "wwwroot");
                string currentAppVersion = VersionTracking.CurrentVersion;
                string lastCopiedVersion = Preferences.Get("WwwRootVersion", "0.0.0");

                if (lastCopiedVersion == currentAppVersion && Directory.Exists(targetDir))
                {
                    Debug.WriteLine("wwwroot cache is up to date.");
                    return;
                }

                Debug.WriteLine("New app version or missing cache. Re-caching wwwroot assets...");
                if (Directory.Exists(targetDir)) Directory.Delete(targetDir, true);
                Directory.CreateDirectory(targetDir);

                // This is the manifest file that lists all other assets.
                const string manifestFile = "filelist.txt";

                // Read the list of asset paths from the manifest.
                string[] assetPaths;
                using (var stream = await FileSystem.OpenAppPackageFileAsync(manifestFile))
                using (var reader = new StreamReader(stream))
                {
                    string content = await reader.ReadToEndAsync();
                    assetPaths = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                }

                // Loop through the dynamic list of assets and copy each one.
                foreach (var logicalPath in assetPaths)
                {
                    if (string.IsNullOrWhiteSpace(logicalPath)) continue;

                    var destinationPath = Path.Combine(targetDir, logicalPath);
                    var destinationDir = Path.GetDirectoryName(destinationPath);
                    if (!Directory.Exists(destinationDir))
                    {
                        Directory.CreateDirectory(destinationDir);
                    }

                    using var assetStream = await FileSystem.OpenAppPackageFileAsync(logicalPath);
                    using var destStream = File.Create(destinationPath);
                    await assetStream.CopyToAsync(destStream);
                }

                Preferences.Set("WwwRootVersion", currentAppVersion);
                Debug.WriteLine("Finished caching wwwroot assets.");
            }
            catch (Exception ex)
            {
                // We throw here so the calling method knows something went wrong.
                Debug.WriteLine($"FATAL: Could not copy wwwroot assets to cache. {ex.Message}");
                throw new Exception("Failed to initialize web assets. Please restart the app.", ex);
            }
            finally
            {
                _isCopying = false;
            }
        }
    }
}