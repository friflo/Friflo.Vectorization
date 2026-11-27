using Avalonia.Controls;
using Demos.BasicGui;
using Friflo.SmGui;

namespace AvaloniaApp;


public partial class MainWindow : Window
{
    public MainWindow() {
        InitializeComponent();
        SkiaGuiView.CreateGuiApp(backend => new BasicGuiApp(backend), new DefaultGuiAssets());
        // SkiaGuiView.CreateGuiApp(backend => new BasicDrawApp(backend), new DefaultGuiAssets());
    }
}