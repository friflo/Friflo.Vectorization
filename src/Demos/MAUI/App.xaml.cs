using Microsoft.Extensions.DependencyInjection;

namespace SkiaMaui;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell()) {
            TitleBar = new TitleBar {
                Title   = "MAUI - SmGui ",
                Icon    = "maui_icon.png" // https://github.com/dotnet/maui/blob/main/Assets/icon.png
            }
        };
    }
}