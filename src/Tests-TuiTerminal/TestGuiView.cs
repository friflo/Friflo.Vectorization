using System.Diagnostics;
using System.Numerics;
using Friflo.TmGui;
using Friflo.TmGui.Session;
using Friflo.TmGui.TUI;

// ReSharper disable ConvertToPrimaryConstructor
namespace TuiTerminal;


public class TestGuiView : IGuiView
{
    private readonly    AppState    appState;
    private readonly    Color32[]   textColors = [0x0000FFFF, 0xFF0000FF, 0x009900FF, 0xFF00FFFF, 0xCC6600FF, 0x000000ff];
    private readonly    TmTexture   myTexture;
    private readonly    TmTexture   canvasTexture;
    private readonly    Stopwatch   stopwatch = Stopwatch.StartNew();
    private             bool        animate;
    
    public TestGuiView(AppState appState, SessionInfo info)
    {
        this.appState   = appState;
        using var stream = typeof(TestGuiView).Assembly.GetManifestResourceStream("TuiTerminal.Assets.sixel_test.png")!;
        myTexture       = info.backend.LoadTexture(stream, "sixel_test.png");
        // var myTextureView    = myTexture.AsImTexture();
        canvasTexture   = info.backend.CreateTexture("canvas", 400, 70, new byte[400 * 70 * 4]);
    }
    
    public void RenderGui(TmBatch batch, int targetWidth, int targetHeight)
    {
        batch.EnableStepRendering = true;
        
        var draw = batch.BeginTextureDraw(canvasTexture);
        ImageDraw(draw);
        
        var gui = batch.BeginGui(targetWidth, targetHeight);
        using (gui.BeginWindow("Window 1", new Vector2(0, 0), new Vector2(1000, 850), traits: 0, TuiBorder.Rounded)) { // (500, 450) (1000, 850)
            Window1(gui);
        }
        using (gui.BeginWindow("Window 2", new(550, 50), new(500, 900))) {
            Window2(gui);
        }
    }
    
    private void Window1(Gui gui)
    {
        gui.Button("hello TUI 💻 🙂", Dim.Fill_X(0, Fit.Content), color: textColors);
        gui.Spacer();
        using (gui.BeginHorizontal()) {
            using (gui.PushStyle(greenButtonStyle)) {
                if (gui.Button("green"))                            Debug.WriteLine("Clicked: hello");
            }
            if (gui.Button("GC.Collect()"))  GC.Collect();
        }

        gui.Spacer();
        gui.Checkbox("mouse circle", ref appState.mouseCircle);
        if(gui.Checkbox("Monocraft", ref appState.monocraft)) {
        }
        gui.Spacer();
        if (gui.Slider("Volume", ref appState.volume, 0f, 1f, 300)) { Debug.WriteLine($"Volume: changed"); }
        gui.Spacer();
        
        gui.BeginHorizontal();
            gui.SetNextDefaultFocus();
            if (gui.Button("First"))                            Debug.WriteLine("Clicked: First");
            gui.Spacer(10);
            if (gui.Button("Second"))                           Debug.WriteLine("Clicked: Second");
            gui.Spacer(10);
            if (gui.Button("Red", style: redButtonStyle))       Debug.WriteLine("Clicked: Red");
        gui.EndHorizontal();
        
        gui.Label("after horizontal", Color32.Teal);
        
        var session = gui.Session;
        if (session != null) {
            animate = session.TickEnabled;
            if (gui.Checkbox("animate", ref animate)) {
                session.TickEnabled = animate;
            }
        }
        gui.Checkbox("terminal pixels", ref appState.useTerminalPixels);
        var canvasSize = new Vector2(384, 70);
        if (appState.useTerminalPixels) canvasSize *= gui.TerminalPixelSize;

        using (var space = gui.BeginSpace(gui.Draw.Tui.ExpandToCellGrid(canvasSize), "canvas")) {
            // var srcPos  = new Vector2(3 * 64, 0 * 64);  // tile pos in Sheet (3, 0)
            // var tint = gui.Colors.ButtonState(space.widgetState);
            gui.Draw.DrawSprite(canvasTexture, space.pos, canvasSize);
        }
        gui.Spacer();
        
        var spriteSize = new Vector2(192, 64);
        if (appState.useTerminalPixels) spriteSize *= gui.TerminalPixelSize;
        
        var spaceSize = gui.Draw.Tui.ExpandToCellGrid(spriteSize);
        // gui.Draw.Tui?.FillRect(gui.widget.Window.Cursor, spaceSize, Color32.Orange);

        using (var space = gui.BeginSpace(spaceSize, "sprite")) {
            // var srcPos  = new Vector2(3 * 64, 0 * 64);  // tile pos in Sheet (3, 0)
            // var tint = gui.Colors.ButtonState(space.widgetState);
            gui.Draw.DrawSprite(myTexture, space.pos, spriteSize);
        }
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
                var buttons = appState.scrollAreaButtons; 
                for (int n = 0; n < 100_000; n++) {
                    buttons.Add($"Added {buttons.Count}");
                }
            }
            if (gui.Button("Clear")) {
                appState.scrollAreaButtons.Clear();
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

            foreach (var button in appState.scrollAreaButtons) {
                gui.Button(button);
            }
        
        gui.EndScrollArea(scrollArea);
        gui.Button("after scroll area");
    }
    
    private void ImageDraw(TmDraw draw)
    {
        draw.FillRect(new Vector2(0, 0), new Vector2(400, 70), 0x000000ff);
        
        draw.FillRect(new Vector2(5, 10), new Vector2(20, 20), 0xff0000ff);
        draw.FillCircle(new Vector2(15, 50), 10, 0x0000ffff);

        draw.FillTriangle(new Vector2(30, 10), new Vector2(50, 10), new Vector2(40, 30), 0x00ff00ff);
        draw.StrokeCircle(new Vector2(40, 50), 10, 2, Color32.Yellow);
        
        draw.FillRectGradientVertical(new Vector2(60, 10), new Vector2(30,40), 0xffffffff, 0xff0000ff);
        
        draw.StrokeLine(new Vector2(100, 10), new Vector2(110, 50), 1, 0xffffffff);
        draw.StrokeLine(new Vector2(110, 10), new Vector2(120, 50), 2, 0xffffffff);
        
        draw.StrokeRect(new Vector2(130, 10), new Vector2(10, 50), 2, Color32.Yellow);
        
        draw.FillRectRounded(new Vector2(150, 10), new Vector2(20, 50), 10, Color32.CornflowerBlue);
        
        draw.StrokeRectRounded(new Vector2(150, 10), new Vector2(20, 50), 10, 2, Color32.White);

        var time = (float)stopwatch.Elapsed.TotalSeconds;

        var x = animate ? MathF.Sin(time * 4) * 60 : 0;

        draw.FillCircle(new Vector2(280 + (int)x, 35), 25, 0xffffffff);
    }
    
}