using System.Diagnostics;
using System.Numerics;
using Friflo.WGPU;
using Friflo.WGPU.ImDraw;

// ReSharper disable ArrangeObjectCreationWhenTypeNotEvident
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable UnusedVariable
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InconsistentNaming
// ReSharper disable ConvertToPrimaryConstructor
namespace TestConsole;

public class ImGuiRenderer : IRenderer
{
    private readonly    Batch2D                 batch;
    private readonly    GpuTexture              myTexture;
    private readonly    GpuTextureView          myTextureView;
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
            ButtonColor = 0x22aa22ff,
            ButtonHover = 0x44cc44ff,
            ButtonDown  = 0x008800ff,
            ButtonText  = 0xffffffff,
            FocusColor  = 0xffffffff,
        }
    };
    
    public void OnShutdown() {
        monocraftFont?.Dispose();
        myTexture.Dispose();
        batch.Dispose();
    }
    
    public ImGuiRenderer(WgpuHost wgpuHost)
    {
        var device = wgpuHost.Device;
        batch = device.CreateBatch2D(wgpuHost.SwapChainFormat);
        
        // create tile texture
        using var stream = typeof(SdlWindow).Assembly.GetManifestResourceStream("Tests-Console.Assets.img.world_tileset.png")!;
        myTexture        = device.LoadTexture(stream, "world_tileset.png"); 
        myTextureView    = myTexture.CreateView();
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
        
        using var gui = batch.BeginGui(target, renderPassDescriptor);
        
        using (gui.BeginWindow("Window 1", new(100, 20), new(500, 700))) {
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
                    monocraftFont ??= target.Device.CreateMonocraftFont(48, 256, 256, 32, 95, "Monocraft");
                    Debug.Assert(monocraftFont.maxY == 244);
                    batch.SetFont(monocraftFont);
                } else {
                    batch.SetFontDefault();
                }
            }
            gui.Spacer();
            if (gui.Slider("Volume", ref volume, 0f, 1f, 200)) Console.WriteLine($"Volume: changed");
            gui.Spacer();
            
            using (gui.BeginHorizontal()) {
                if (gui.Button("Left"))                             Console.WriteLine("Clicked: Left");
                gui.Spacer(15);
                if (gui.Button("Right"))                            Console.WriteLine("Clicked: Right");
                gui.Spacer(15);
                if (gui.Button("Red", redButtonStyle))              Console.WriteLine("Clicked: Red");
            }
        }
        
        using (var isOpen = gui.BeginWindow("Window 2", new(650, 20), new(500, 600))) {
            gui.Checkbox("checkbox", ref enabled2);
            gui.Spacer();
            using (var space = gui.BeginSpace(new(64, 64), "sprite")) {
                if (space.isFired) Console.WriteLine("Clicked: Sprite");
                var srcPos  = new Vector2(3 * 64, 3 * 64);  // tile pos in Sheet (6,2)        
                var tint = gui.Color.ButtonState(space.widgetState);
                gui.Draw.DrawSpriteRegion(myTextureView, space.pos, space.size, srcPos, space.size, new(1024, 1024), tint);
            }
        }
        
        if (mouseCircle) {
            using (gui.Draw.PushZIndex(10)) {
                gui.Draw.StrokeCircle(batch.input.Mouse, radius: 40f, 4, color: 0xFF0000FF, segments: 32);
            }
        }
    }
}


public static class GuiExtensions
{
    public static bool MyButton(this in Gui gui, ReadOnlySpan<char> name, GuiStyle? style = null, WidgetID id = default)
    {
        var widget  = gui.widget;
        var draw    = gui.Draw;
        var window  = widget.Window;
        using var __ = widget.UseStyle(style);
        
        int parentHash  = window.GetCurrentScopeHash();
        int widgetId    = id.Resolve(name, parentHash);
        
        var size    = draw.MeasureText(name);
        var isHover = window.IsHoverAtCursor(size, draw);

        // Calculate widget center & register for 1D/2D navigation
        var center      = window.Cursor + size * 0.5f;
        var isFocused   = widget.RegisterFocusable(widgetId, center, out _);

        var widgetState = widget.GetWidgetState(isHover, widgetId);
        
        draw.FillRectRounded(window.Cursor, size, 8, widget.Color.ButtonState(widgetState)); // background

        if (isFocused) {
            draw.StrokeRect(window.Cursor, size, 4, widget.Color.FocusColor);
        }
        draw.DrawTextInRect(name, window.Cursor, size, TextAlignment.Center, VerticalAlignment.Middle, widget.Color.ButtonText);
        
        window.MoveCursor(size);
        return widget.IsFired(widgetState, isFocused);
    }
}
