using System.Diagnostics;
using System.Numerics;
using Friflo.WGPU;
using Friflo.WGPU.ImDraw;

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
            ButtonText  = 0xffffffff
        }
    };
    
    private readonly GuiStyle greenButtonStyle = new() {
        color = new GuiColor {
            ButtonColor = 0x22aa22ff,
            ButtonHover = 0x44cc44ff,
            ButtonDown  = 0x008800ff,
            ButtonText  = 0xffffffff
        }
    };
    
    public void OnShutdown() {
        monocraftFont?.Dispose();
        myTexture.Dispose();
        batch.Dispose();
    }
    
    public ImGuiRenderer(Wgpu wgpu)
    {
        var device = wgpu.Device;
        batch = device.CreateBatch2D(wgpu.SwapChainFormat);
        
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
            clearValue  = [0.1, 0.1, 0.1, 1]
        };
    }

    public void OnEvent(in ImEvent ev) => batch.AddEvent(ev);

    public void OnFrame(in RenderFrame frame)
    {
        perfLog.Trace(10000);
        
        batch.input.NewFrame();
        using var gui = batch.BeginGui(frame, renderPassDescriptor);
        
        gui.SetNextWindowPos(new Vector2(100, 20));
        gui.SetNextWindowSize(new Vector2(500, 700));
        using (gui.BeginWindow("Window 1")) {
            gui.Label("hello GUI");
            gui.Label("");
            using (gui.PushStyle(greenButtonStyle)) {
                if (gui.Button("hello"))                            Console.WriteLine("Clicked: hello");
                if (gui.Button("world", id: 0x7777ffff))            Console.WriteLine("Clicked: world");
            }
            gui.Label("");
            gui.Checkbox("mouse circle", ref mouseCircle);
            if(gui.Checkbox("Monocraft", ref monocraft)) {
                if (monocraft) {
                    monocraftFont ??= frame.Device.CreateMonocraftFont(48, 256, 256, 32, 95, "Monocraft");
                    Debug.Assert(monocraftFont.maxY == 244);
                    batch.SetFont(monocraftFont);
                } else {
                    batch.SetFontDefault();
                }
            }
            gui.Label("");
            if (gui.Slider(200, "Volume", ref volume, "F1", 0f, 1f)) Console.WriteLine($"Volume: changed");
            gui.Label("");
            
            using (gui.BeginHorizontal()) {
                if (gui.Button("Left"))                             Console.WriteLine("Clicked: Left");
                if (gui.Button("Right"))                            Console.WriteLine("Clicked: Right");
                if (gui.Button("Red", redButtonStyle))              Console.WriteLine("Clicked: Red");
            }
        }
        
        gui.SetNextWindowPos(new Vector2(650, 20));
        gui.SetNextWindowSize(new Vector2(500, 600));
        using (var isOpen = gui.BeginWindow("Window 2")) {
            gui.Checkbox("checkbox", ref enabled2);
            gui.Label("");
            
            var srcPos  = new Vector2(3 * 64, 3 * 64);  // tile pos in Sheet (6,2)        
            var srcSize = new Vector2(64, 64);          // 64x64 Tile
            if (gui.ReserveSpace(out var spritePos, srcSize, out var isFocused, out _, "sprite"))   Console.WriteLine("Clicked: Sprite");
            gui.draw.DrawSprite(spritePos, srcSize, myTextureView, srcPos, srcSize, new Vector2(1024, 1024));
            gui.DrawFocusRect(spritePos, srcSize, isFocused);
        }
        
        if (mouseCircle) {
            gui.draw.PushZIndex(10);
            gui.draw.CircleLines(batch.input.Mouse, radius: 40f, 4, color: 0xFF0000FF, segments: 32);
        }
        Sdl3Cursor.SetCursor(batch.input.CurrentCursor);
    }
}
