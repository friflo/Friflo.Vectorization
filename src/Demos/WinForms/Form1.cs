using Demos.BasicGui;
using Friflo.SmGui;
using Friflo.SmGui.WinForms;

namespace WinFormsApp;

public partial class Form1 : Form
{
    public Form1() {
        InitializeComponent();
        
        // add SkiaGuiControl programmatically
        // var guiControl = new SkiaGuiControl { Dock = DockStyle.Fill};
        // Controls.Add(guiControl);

        // guiControl can be added programmatically of via WinForms Designer
        guiControl.CreateGuiApp(backend => new BasicGuiApp(backend), new DefaultGuiAssets());
    }
}