using System.Diagnostics;
using System.Numerics;
using Friflo.GPU;
using Friflo.WGPU;
using Friflo.ImGui;

// ReSharper disable ArrangeObjectCreationWhenTypeNotEvident
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable UnusedVariable
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InconsistentNaming
// ReSharper disable ConvertToPrimaryConstructor
namespace TestConsole;

public class ImGuiRenderer : IRenderer
{
    private readonly    GpuDevice               device;
    private readonly    WgpuGuiBackend          guiBackend;
    private readonly    Batch2D                 batch;
    private readonly    GpuTexture              myTexture;
    private readonly    ImTexture               myTextureView;
    private readonly    GpuRenderPassDescriptor renderPassDescriptor    = new () { colorAttachments = [ default ] };
    private readonly    PerfLog                 perfLog                 = new();
    private             bool                    mouseCircle;
    private             bool                    monocraft;
    private             Font?                   monocraftFont;
    private             bool                    enabled2;
    private             float                   volume;
    
    private readonly GuiStyle redButtonStyle = new() {
        color = new GuiColor {
            ButtonColor = 0xaa4444ff,
            ButtonHover = 0xcc6666ff,
            ButtonDown  = 0x882222ff,
            ButtonText  = 0xffffffff,
            FocusColor  = 0xffffffff,
        }
    };
    
    private readonly GuiStyle greenButtonStyle = new() {
        color = new GuiColor {
            ButtonColor = 0x229922ff,
            ButtonHover = 0x44bb44ff,
            ButtonDown  = 0x007700ff,
            ButtonText  = 0xffffffff,
            FocusColor  = 0xffffffff,
        }
    };
    
    public void OnShutdown() {
        monocraftFont?.Dispose();
        myTexture.Dispose();
        batch.Dispose();
        guiBackend.Dispose();
    }
    
    public ImGuiRenderer(WgpuHost wgpuHost)
    {
        device = wgpuHost.Device;
        guiBackend = new WgpuGuiBackend(device);
        batch  = guiBackend.CreateBatch2D(guiBackend, wgpuHost.SwapChainFormat);
        
        // create tile texture
        using var stream = typeof(SdlWindow).Assembly.GetManifestResourceStream("Tests-Console.Assets.img.world_tileset.png")!;
        myTexture        = guiBackend.LoadTexture(stream, "world_tileset.png"); 
        myTextureView    = myTexture.CreateView().ToImTexture();
    }
    
    public void OnWindowChanged(int width, int height)
    {
        renderPassDescriptor.colorAttachments[0] = new GpuRenderPassColorAttachment {
            loadOp      = LoadOp.Clear,
            storeOp     = StoreOp.Store,
            clearValue  = new GpuColor(0.1f, 0.1f, 0.1f, 1f)
        };
    }

    public void OnFrame(in RenderTarget target)
    {
        perfLog.Trace(10000);
        
        var gui = batch.BeginGui(target.Width, target.Height);
        
        using (gui.BeginWindow("Window 1", new(100, 20), new(400, 950))) {
            Window1(gui); 
        }
        using (var isOpen = gui.BeginWindow("Window 2", new(550, 20), new(500, 900))) {
            Window2(gui);
        }
        if (mouseCircle) {
            using (gui.Draw.PushZIndex(10)) {
                gui.Draw.StrokeCircle(batch.input.MousePos, radius: 40f, 4, color: 0xFF0000FF, segments: 32);
            }
        }
        gui.Draw.DrawCommandList(target, renderPassDescriptor);
    }
    
    private void Window1(Gui gui)
    {
        gui.Label("hello GUI");
        gui.Spacer();
        using (gui.PushStyle(greenButtonStyle)) {
            if (gui.Button("hello"))                            Console.WriteLine("Clicked: hello");
        }
        if (gui.MyButton("MyButton", id: 0x7777ffff))           Console.WriteLine("Clicked: MyButton");

        gui.Spacer();
        gui.Checkbox("mouse circle", ref mouseCircle);
        if(gui.Checkbox("Monocraft", ref monocraft)) {
            if (monocraft) {
                monocraftFont ??= guiBackend.CreateMonocraftFont(48, 256, 256, 32, 95, "Monocraft");
                Debug.Assert(monocraftFont.maxY == 244);
                batch.SetFont(monocraftFont);
            } else {
                batch.SetFontDefault();
            }
        }
        gui.Spacer();
        if (gui.Slider("Volume", ref volume, 0f, 1f, 200)) Console.WriteLine($"Volume: changed");
        gui.Spacer();
        
        gui.BeginHorizontal();
            if (gui.Button("First"))                            Console.WriteLine("Clicked: First");
            gui.Spacer(10);
            if (gui.Button("Second"))                           Console.WriteLine("Clicked: Second");
            gui.Spacer(10);
            if (gui.Button("Red", redButtonStyle))              Console.WriteLine("Clicked: Red");
        gui.EndHorizontal();
        
        gui.Label("after horizontal");
        using (var space = gui.BeginSpace(new(64, 64), "sprite")) {
            if (space.isFired) Console.WriteLine("Clicked: Sprite");
            var srcPos  = new Vector2(3 * 64, 3 * 64);  // tile pos in Sheet (6,2)        
            var tint = gui.Color.ButtonState(space.widgetState);
            gui.Draw.DrawSpriteRegion(myTextureView, space.pos, space.size, srcPos, space.size, new(1024, 1024), tint);
        }
        gui.Spacer();
        gui.Checkbox("checkbox", ref enabled2);
        
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
        using (gui.BeginChild(1, new Vector2(250, 90))) {
            gui.Button("Button 1 clipped");
            gui.Button("Button 2 clipped");
        }
        gui.Spacer();
        gui.Label("auto-fit child");
        using (gui.BeginChild(2, new Vector2(0, 0))) {
            gui.Button("Button 1 unclipped");
            gui.Button("Button 2 unclipped");
        }
        gui.Spacer();
        gui.Label("scroll area");
        var scrollArea = gui.BeginScrollArea(3, new Vector2(450, 350));
            gui.Button("Button 1 - more to to enable horizontal scrolling");
            gui.Button("Button 2");
            
            var area2  = gui.BeginScrollArea(4, new Vector2(400, 200));
                gui.Button("Sub 1");
                gui.Button("Sub 2");
                gui.Button("Sub 3");
                gui.Button("Sub 4");
                gui.Button("Sub 5");
                gui.Button("Sub 6");
                gui.Button("Sub 7");
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
                gui.Button("Hori G");
                gui.Button("Hori H");
            gui.EndHorizontal();
            gui.Button("Button 5");
            gui.Button("Button 6");
            gui.Button("Button 7");
            gui.Button("Button 8");
            gui.Button("Button 9");
        
        gui.EndScrollArea(scrollArea);
    }
}


public static class GuiExtensions
{
    public static bool MyButton(this in Gui gui, ReadOnlySpan<char> name, GuiStyle? style = null, WidgetID id = default)
    {
        var widget  = gui.widget;
        var draw    = gui.Draw;
        var window  = widget.Window;
        using var _ = widget.UseStyle(style);
        
        int parentHash  = window.GetCurrentScopeHash();
        int widgetId    = id.Resolve(name, parentHash);
        
        var size        = draw.MeasureText(name);
        var pos         = window.Cursor;
        var isHover     = window.IsHoverAtCursor(size, draw);
        var isFocused   = widget.RegisterFocusable(widgetId, pos, size);
        var widgetState = widget.GetWidgetState(isHover, widgetId);
        
        draw.FillRectRounded(pos, size, 8, widget.Color.ButtonState(widgetState)); // background

        if (isFocused) {
            draw.StrokeRect(pos, size, 4, widget.Color.FocusColor);
            window.EnsureVisibleInScrollArea(pos, size);
        }
        draw.DrawTextInRect(name, pos, size, TextAlignment.Center, VerticalAlignment.Middle, widget.Color.ButtonText);
        
        window.MoveCursor(size);
        return widget.IsFired(widgetState, isFocused);
    }
}
