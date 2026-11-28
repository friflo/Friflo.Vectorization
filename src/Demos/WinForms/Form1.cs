using Demos.BasicGui;
using Friflo.SmGui;

namespace WinFormsApp;

public partial class Form1 : Form
{
    // SkiaGuiControl guiControl;  // add SkiaGuiControl programmatically 
    
    public Form1() {
        InitializeComponent();
        // guiControl can be added programmatically of via WinForms Designer
        guiControl.CreateGuiApp(backend => new BasicGuiApp(backend), new DefaultGuiAssets());
    }
}