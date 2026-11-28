using Friflo.SmGui.WinForms;

namespace WinFormsApp;

public partial class Form1 : Form
{
    public Form1() {
        InitializeComponent();
        var gui = new SkiaGuiControl { Dock = DockStyle.Fill };
        Controls.Add(gui);
    }
}