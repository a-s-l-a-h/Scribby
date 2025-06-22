using Microsoft.Extensions.Logging;
using ScribbyApp.Services;
using ScribbyApp.Views;

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

        // Singleton for main tab pages that should persist
        builder.Services.AddSingleton<HomePage>();
        builder.Services.AddSingleton<ConnectPage>();
        builder.Services.AddSingleton<ChatLLMPage>(); // <-- MODIFIED: Now a singleton for persistence

        // Transient for pages we navigate to, creating a new instance each time
        builder.Services.AddTransient<ControlPage>();
        builder.Services.AddTransient<ScriptPage>();

        return builder.Build();
    }
}