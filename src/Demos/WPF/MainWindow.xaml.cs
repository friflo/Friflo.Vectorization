using System.Windows;
using Demos.BasicGui;
using Friflo.SmGui;

namespace WPFApp;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow() {
        InitializeComponent();
        GuiElement.CreateGuiApp(backend => new BasicGuiApp(backend), new DefaultGuiAssets());
    }
}