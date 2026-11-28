using Demos.BasicDraw;
using Demos.BasicGui;
using Friflo.SmGui;

namespace SkiaMaui;

public partial class MainPage
{
    public MainPage()
    {
        InitializeComponent();
        
        GuiView.CreateGuiApp(backend => new BasicGuiApp(backend), new DefaultGuiAssets());
        // GuiView.CreateGuiApp(backend => new BasicDrawApp(backend), new DefaultGuiAssets());
    }
}