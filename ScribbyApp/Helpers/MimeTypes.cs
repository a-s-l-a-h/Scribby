// Helpers/MimeTypes.cs
public static class MimeTypes
{
    private static readonly Dictionary<string, string> MimeTypeMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { ".html", "text/html" },
        { ".htm", "text/html" },
        { ".js", "application/javascript" },
        { ".css", "text/css" },
        { ".png", "image/png" },
        { ".jpg", "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".gif", "image/gif" },
        { ".svg", "image/svg+xml" },
        { ".woff", "font/woff" },
        { ".woff2", "font/woff2" },
        { ".ttf", "font/ttf" },
    };

    public static string GetMimeType(string fileName)
    {
        string extension = Path.GetExtension(fileName);
        if (extension != null && MimeTypeMappings.TryGetValue(extension, out var mimeType))
        {
            return mimeType;
        }
        return "application/octet-stream"; // Default fallback
    }
}