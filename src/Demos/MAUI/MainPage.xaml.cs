using Demos.BasicDraw;
using Demos.BasicGui;
using Friflo.SmGui;

namespace SkiaMaui;

public partial class MainPage
{
    public MainPage()
    {
        InitializeComponent();
        
        SkiaGuiView.CreateGuiApp(backend => new BasicGuiApp(backend), new DefaultGuiAssets());
        // SkiaGuiView.CreateGuiApp(backend => new BasicDrawApp(backend), new DefaultGuiAssets());
    }
}