using Demos.BasicGui;

namespace SkiaMaui;

public partial class MainPage
{
    public MainPage()
    {
        InitializeComponent();
        
        var app = new BasicGuiApp(SkiaView.GuiBackend);
        SkiaView.AttachApp(app);
    }
}