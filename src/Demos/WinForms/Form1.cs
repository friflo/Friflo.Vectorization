using Demos.BasicGui;
using Friflo.SmGui;
using Friflo.SmGui.WinForms;

namespace WinFormsApp;

public partial class Form1 : Form
{
    public Form1() {
        InitializeComponent();
        Controls.Add(guiControl);
        guiControl.CreateGuiApp(backend => new BasicGuiApp(backend), new DefaultGuiAssets());
    }
}