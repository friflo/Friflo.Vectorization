using System.Numerics;
using Friflo.TmGui;
using Friflo.TmGui.TUI.Terminal;

namespace TerminalServer;


public class TestRenderer : IGuiView
{
    private bool    mouseCircle;
    private bool    monocraft;
    private bool    enabled2;
    private float   volume = 0.8f;
    
    public void RenderGui(TmBatch batch, int targetWidth, int targetHeight)
    {
        var gui = batch.BeginGui(targetWidth, targetHeight);
        
        using (gui.BeginWindow("Window 1", new Vector2(50, 50), new Vector2(800, 950))) {
            Window1(gui);
        }
    }
    
    private void Window1(Gui gui)
    {
        gui.Button("hello GUI", Dim.Fill_X(0, Fit.Content));
        gui.Spacer();
        using (gui.PushStyle(greenButtonStyle)) {
            if (gui.Button("hello"))                            Console.WriteLine("Clicked: hello");
        }

        gui.Spacer();
        gui.Checkbox("mouse circle", ref mouseCircle);
        if(gui.Checkbox("Monocraft", ref monocraft)) {
        }
        gui.Spacer();
        if (gui.Slider("Volume", ref volume, 0f, 1f, 300)) Console.WriteLine($"Volume: changed");
        gui.Spacer();
        
        gui.BeginHorizontal();
            gui.SetNextDefaultFocus();
            if (gui.Button("First"))                            Console.WriteLine("Clicked: First");
            gui.Spacer(10);
            if (gui.Button("Second"))                           Console.WriteLine("Clicked: Second");
            gui.Spacer(10);
            if (gui.Button("Red", style: redButtonStyle))       Console.WriteLine("Clicked: Red");
        gui.EndHorizontal();
        
        gui.Label("after horizontal");
        using (var space = gui.BeginSpace(new(64, 64), "sprite")) {
            if (space.isFired) Console.WriteLine("Clicked: Sprite");
        }
        gui.Spacer();
        gui.Checkbox("checkbox", ref enabled2);
        
        gui.Spacer();
        
        using (gui.BeginHorizontalAligned(47, HorizontalAlignment.Right)) {
            gui.Button("Right");
            gui.Button("A");
            gui.Button("B");
            gui.Button("C");
        }
        using (gui.BeginHorizontalAligned(11, HorizontalAlignment.Center)) {
            gui.Button("Center");
            gui.Button(" 1 ");
            gui.Button(" 2 ");
            gui.Button(" 3 ");
        }
        using (gui.BeginHorizontal()) {
            gui.Button("usual");
            gui.Button("horizontal");
            gui.Button(" A ");
            gui.Button(" B ");
            gui.Button(" C ");
        }
    }
    
    private readonly GuiStyle redButtonStyle = new() {
        colors = new GuiColors {
            ButtonColor = 0xaa4444ff,
            ButtonHover = 0xcc6666ff,
            ButtonDown  = 0x882222ff,
            ButtonText  = 0xffffffff,
            FocusColor  = 0xffffffff,
        }
    };
    
    private readonly GuiStyle greenButtonStyle = new() {
        colors = new GuiColors {
            ButtonColor = 0x229922ff,
            ButtonHover = 0x44bb44ff,
            ButtonDown  = 0x007700ff,
            ButtonText  = 0xffffffff,
            FocusColor  = 0xffffffff,
        }
    };
}