using Demos.BasicGui;
using Friflo.SmGui;
using Friflo.SmGui.Uno;

namespace UnoApp;

public sealed partial class MainPage : Page
{
    public MainPage() {
        InitializeComponent();
        SkiaGuiView.CreateGuiApp(backend => new BasicGuiApp(backend), new DefaultGuiAssets());
    }
}
