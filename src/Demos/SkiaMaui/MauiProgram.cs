using Microsoft.Extensions.Logging;
using MyApp;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace SkiaMaui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseSkiaSharp() // register handler for SKCanvasView
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });
        builder.UseKeyHandler();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}