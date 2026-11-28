using Avalonia.Controls;
using Demos.BasicGui;
using Friflo.SmGui;

namespace AvaloniaApp;


public partial class MainWindow : Window
{
    public MainWindow() {
        InitializeComponent();
        GuiControl.CreateGuiApp(backend => new BasicGuiApp(backend), new DefaultGuiAssets());
        // GuiControl.CreateGuiApp(backend => new BasicDrawApp(backend), new DefaultGuiAssets());
    }
}