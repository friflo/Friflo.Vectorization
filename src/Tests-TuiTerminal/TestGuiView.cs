using System.Numerics;
using Friflo.TmGui;

// ReSharper disable ConvertToPrimaryConstructor
namespace TuiTerminal;


public class TestGuiView : IGuiView
{
    private readonly    AppState    appState;
    private readonly    Color32[]   textColors = [0x0000FFFF, 0xFF0000FF, 0x009900FF, 0xFF00FFFF, 0xCC6600FF, 0x000000ff];
    
    public TestGuiView(AppState appState)
    {
        this.appState = appState;
    }
    
    public void RenderGui(TmBatch batch, int targetWidth, int targetHeight)
    {
        var gui = batch.BeginGui(targetWidth, targetHeight);
        
        using (gui.BeginWindow("Window 1", new Vector2(0, 0), new Vector2(1000, 850), TmTrait.Border)) { // (500, 450) (1000, 850)
            Window1(gui);
        }
        using (gui.BeginWindow("Window 2", new(550, 50), new(500, 900))) {
            Window2(gui);
        }
    }
    
    private void Window1(Gui gui)
    {
        gui.Button("hello GUI", Dim.Fill_X(0, Fit.Content), color: textColors);
        gui.Spacer();
        using (gui.PushStyle(greenButtonStyle)) {
            if (gui.Button("hello"))                            Console.WriteLine("Clicked: hello");
        }

        gui.Spacer();
        gui.Checkbox("mouse circle", ref appState.mouseCircle);
        if(gui.Checkbox("Monocraft", ref appState.monocraft)) {
        }
        gui.Spacer();
        if (gui.Slider("Volume", ref appState.volume, 0f, 1f, 300)) Console.WriteLine($"Volume: changed");
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
        using (var space = gui.BeginSpace(new(128, 64), "sprite")) {
            if (space.isFired) Console.WriteLine("Clicked: Sprite");
            var srcPos  = new Vector2(3 * 64, 0 * 64);  // tile pos in Sheet (3, 0)        
            var tint = gui.Colors.ButtonState(space.widgetState);
            gui.Draw.DrawSpriteRegion(default, space.pos, space.size, srcPos, space.size, new(1024, 1024), tint);
        }
        gui.Spacer();
        gui.Checkbox("checkbox", ref appState.enabled2);
        
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
    
    private static void Window2(Gui gui)
    {
        gui.Label("fixed child");
        using (gui.BeginChild(1, Dim.Fill_X(0, 90))) {
            gui.Button("Button 1 clipped", Dim.Fill_X(0, Fit.Content));
            gui.Button("Button 2 clipped");
        }
        gui.Spacer();
        gui.Label("auto-fit child");
        using (gui.BeginChild(2, Dim.Fill())) {
            gui.Button("Button 1 unclipped",  Dim.Fill_X(0, Fit.Content));
            gui.Button("Button 2 unclipped");
        }
        gui.Spacer();
        gui.Label("scroll area");
        var scrollArea = gui.BeginScrollArea(3, Dim.Fill(0, 60));
            gui.Button("Button 1 - more to to enable horizontal scrolling");
            gui.Button("Button 2 -  Dim.Fill_X(0, Fit.Content)",  Dim.Fill_X(0, Fit.Content));
            
            var area2  = gui.BeginScrollArea(4, Dim.Fill_X(0, 200));
                gui.Button("Sub 1");
                gui.Button("Sub 2 - Dim.Fill_X(0, Fit.Content)",   Dim.Fill_X(0, Fit.Content));
                gui.Button("Sub 3 - Dim.Fill_X(20, Fit.Content)",  Dim.Fill_X(20, Fit.Content));
                gui.Button("Sub 4");
                gui.Button("Sub 5");
                gui.Button("Sub 6");
                gui.Button("Sub last");
            gui.EndScrollArea(area2);
            
            gui.Button("Button 3");
            gui.Button("Button 4");
            gui.BeginHorizontal();
                gui.Button("Hori A");
                gui.Button("Hori B");
                gui.Button("Hori C");
                gui.Button("Hori D");
                gui.Button("Hori E");
                gui.Button("Hori F");
            gui.EndHorizontal();
            gui.Button("Button 5");
            gui.Button("Button 6");
            gui.Button("Button 7");
            gui.Button("Button 8");
            using (gui.BeginHorizontalAligned(123, HorizontalAlignment.Right)) {
                gui.Button("Right");
            }
            using (gui.BeginHorizontalAligned(456, HorizontalAlignment.Center)) {
                gui.Button("Center");
            }
            gui.Button("Button last");
        
        gui.EndScrollArea(scrollArea);
        gui.Button("after scroll area");
    }
}