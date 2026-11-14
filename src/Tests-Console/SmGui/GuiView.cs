// --- GuiView.cs

using System.Numerics;
using Friflo.SmGui;

namespace TestConsole;


public class GuiView : IGuiView
{
    private readonly    AppResources    resources;
    private             bool            mouseCircle;
    private             bool            monocraft;
    private             bool            fullScreen;
    private             int             helloCounter;
    private             float           volume = 0.8f;
    private readonly    List<string>    scrollAreaButtons = [];
    private readonly    Color32[]       textColors = [0x0000FFFF, 0xFF0000FF, 0x009900FF, 0xFF00FFFF, 0xCC6600FF, 0x000000ff];
    
    
    public GuiView(AppResources resources)
    {
        this.resources  = resources;
    }
    
    public GuiView(AppResources resources, ViewSession session)
    {
        this.resources  = resources;
        
        session.Action.SetWindowTitle("Web client");
        session.Action.SetClearColor(0x999c9eff, 0x404142ff); // same color as clearValue
    }
    
    private readonly GuiStyle redButtonStyle = new() {
        colors = new GuiColors {
            ButtonColor = 0xaa4444ff,
            ButtonBorder= 0, 
            ButtonHover = 0xcc6666ff,
            ButtonDown  = 0x882222ff,
            ButtonText  = 0xffffffff,
            FocusColor  = 0xaa4444ff,
        }
    };
    
    private readonly GuiStyle greenButtonStyle = new() {
        colors = new GuiColors {
            ButtonColor = 0x229922ff,
            ButtonBorder= 0,
            ButtonHover = 0x44bb44ff,
            ButtonDown  = 0x007700ff,
            ButtonText  = 0xffffffff,
            FocusColor  = 0x229922ff,
        }
    };
    
    public void RenderView(SmBatch batch, int width, int height)
    {
        var gui = batch.BeginGui(width, height);
        
        using (gui.BeginWindow("Window 1", new(100, 20), new(400, 950))) {
            Window1(gui); 
        }
        using (var isOpen = gui.BeginWindow("Window 2", new(550, 20), new(500, 900))) {
            Window2(gui);
        }
        if (mouseCircle) {
            using (gui.Draw.PushZIndex(10)) {
                gui.Draw.StrokeCircle(gui.Input.MousePos, radius: 40f, 4, color: 0xFF0000FF, segments: 32);
            }
        }
    }
    
    private void Window1(Gui gui)
    {
        gui.Button("hello GUI", Dim.Fill_X(0, Fit.Content), color: textColors);
        if (gui.Button("Web client")) {
            gui.Action.OpenUrl($"https://localhost:{SmGuiRenderer.Port}", "remote-gui");
        }
        if (gui.UiType != UiType.WebGui) {
            if (gui.Button("close session")) gui.Action.CloseWindow();
        }
        if (gui.Button("copy to clipboard")) {
            gui.Action.CopyToClipboard("some clipboard text");
        }
        gui.Spacer();
        using (gui.PushStyle(greenButtonStyle)) {
            if (gui.Button("hello"))                            gui.Action.SetWindowTitle($"hello {++helloCounter}");
        }
        if (gui.MyButton("MyButton", id: 0x7777ffff))           Console.WriteLine("Clicked: MyButton");

        gui.Spacer();
        gui.Checkbox("mouse circle", ref mouseCircle);
        if(gui.Checkbox("Monocraft", ref monocraft)) {
            if (monocraft) {
                gui.Batch.SetFont(resources.monocraftFont);
            } else {
                gui.Batch.SetFontDefault();
            }
        }
        gui.Spacer();
        if (gui.Slider("Volume", ref volume, 0f, 1f, 300)) Console.WriteLine($"Volume: changed");
        gui.Spacer();
        
        gui.BeginHorizontal();
            gui.SetNextDefaultFocus();
            if (gui.Button("First"))                            Console.WriteLine("Clicked: First");
            if (gui.Button("Second"))                           Console.WriteLine("Clicked: Second");
            if (gui.Button("Red", style: redButtonStyle))       Console.WriteLine("Clicked: Red");
        gui.EndHorizontal();
        
        gui.Label("after horizontal", Color32.Teal);
        using (var space = gui.BeginSpace(new(128, 64), "sprite")) {
            if (space.IsFired) Console.WriteLine("Clicked: Sprite");
            var srcPos  = new Vector2(3 * 64, 0 * 64);  // tile pos in Sheet (3, 0)        
            var tint = gui.Colors.ButtonState(space.WidgetState);
            gui.Draw.DrawSpriteRegion(resources.myTexture, space.Pos, space.Size, srcPos, space.Size, new(1024, 1024), tint);
        }
        gui.Spacer();
        if (gui.Checkbox("fullScreen", ref fullScreen)) {
            gui.Action.SetDisplayMode(fullScreen ? SmDisplayMode.Fullscreen : SmDisplayMode.Windowed);
        }
        
        gui.Spacer();
        
        using (gui.BeginHorizontalAligned(47, HorizontalAlignment.Right)) {
            gui.Button("Right");
            gui.Button(" A´");
            gui.Button(" B´");
            gui.Button(" C´ ");
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
    
    private void Window2(Gui gui)
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
        using (gui.BeginHorizontal()) {
            gui.Label("scroll area");
            if (gui.Button("Add 100.000")) {
                var buttons = scrollAreaButtons; 
                for (int n = 0; n < 100_000; n++) {
                    buttons.Add($"Added {buttons.Count}");
                }
            }
            if (gui.Button("Clear")) {
                scrollAreaButtons.Clear();
            }
        }
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
            foreach (var button in scrollAreaButtons) {
                gui.Button(button);
            }
        
        gui.EndScrollArea(scrollArea);
        gui.Button("after scroll area");
    }
}