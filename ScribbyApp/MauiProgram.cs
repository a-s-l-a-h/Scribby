using Microsoft.Extensions.Logging;
using ScribbyApp.Services;
using ScribbyApp.Views;
using ScribbyApp.Models;

namespace ScribbyApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("RobotoMono-Regular.ttf", "RobotoMono");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // Register services and pages
        builder.Services.AddSingleton<BluetoothService>();
        builder.Services.AddSingleton<DatabaseService>();

        // Singleton for main tab pages that should persist
        builder.Services.AddSingleton<HomePage>();
        builder.Services.AddSingleton<ConnectPage>();

        // Transient for pages we navigate to, creating a new instance each time
        builder.Services.AddTransient<ControlPage>();
        builder.Services.AddSingleton<CodeListPage>();

        builder.Services.AddTransient<CodeEditorPage>();
        builder.Services.AddTransient<CodePreviewPage>();

        return builder.Build();
    }
}