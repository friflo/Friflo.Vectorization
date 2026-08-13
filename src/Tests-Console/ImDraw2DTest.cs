using System.Diagnostics;
using System.Numerics;
using Friflo.Vectorization.WebGPU;
using Friflo.WGPU.ImDraw;
using StbImageSharp;


// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InconsistentNaming
// ReSharper disable ConvertToPrimaryConstructor
namespace TestConsole;

public class ImRenderer : IRenderer
{
    private readonly    Batch2D                 batch;
    private readonly    GpuTexture              myTexture;
    private readonly    GpuTextureView          myTextureView;
    private readonly    GpuRenderPassDescriptor renderPassDescriptor    = new () { colorAttachments = [ default ] };
    private readonly    Stopwatch               stopwatch               = Stopwatch.StartNew();
    private             float                   lastTime;
    private             float                   rotation;
    private readonly    PerfLog                 perfLog                 = new();
    
    public void OnShutdown() {
        myTexture.Dispose();
        batch.Dispose();
    }
    
    public ImRenderer(Wgpu wgpu)
    {
        var device = wgpu.Device;
        batch = new Batch2D(device, wgpu.SwapChainFormat, FilterMode.Linear);
        
        // create tile texture
        using var stream = typeof(SdlWindow).Assembly.GetManifestResourceStream("Tests-Console.Assets.img.world_tileset.png");
        var image   = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
        myTexture   = device.CreateTexture(new GpuTextureDescriptor { label = "world_tileset.png", 
            size    = [image.Width, image.Height],
            format  = TextureFormat.RGBA8Unorm,
            usage   = TextureUsage.TextureBinding | TextureUsage.CopyDst
        });
        myTexture.Write(image.Data, bytesPerRow: image.Width * 4, rowsPerImage: image.Height);  // 1024 x 1024
        myTextureView = myTexture.CreateView();
    }
    
    public void OnWindowChanged(int width, int height)
    {
        renderPassDescriptor.colorAttachments[0] = new GpuRenderPassColorAttachment {
            loadOp      = LoadOp.Clear,
            storeOp     = StoreOp.Store,
            clearValue  = [0.1, 0.1, 0.1, 1]
        };
    }
    
    public void OnFrame(in RenderFrame frame)
    {
        perfLog.Trace(5000);
        var currentTime = (float)stopwatch.Elapsed.TotalSeconds;
        var deltaTime   = currentTime - lastTime;
        lastTime        = currentTime;
        
        using var draw = batch.BeginDraw2D(frame, renderPassDescriptor);
        
        DrawSprites(draw, deltaTime, frame.Width, frame.Height);
        DrawShapes(draw);
        DrawText(draw);
        
        draw.Flush();
    }
    
    public void DrawSprites(Draw2D draw, float deltaTime, int width, int height)
    {
        draw.Rectangle(new Vector2(1, 1), new Vector2(99, 99), 0xFFFFFFFF);
        draw.Rectangle(new Vector2(width - 100, height - 100), new Vector2(99, 99), 0xFFFFFFFF);
        
        draw.Rectangle(new Vector2(200, 50), new Vector2(50, 50), Color32.Red);
        draw.Rectangle(new Vector2(300, 50), new Vector2(50, 50), Color32.Green);
        draw.Rectangle(new Vector2(400, 50), new Vector2(50, 50), Color32.Blue);
        
        // --- sprites
        draw.DrawSprite(new Vector2( 50, 150), new Vector2(256, 256), myTextureView);
        draw.DrawSprite(new Vector2(200, 150), new Vector2(256, 256), myTextureView, uvMin: new Vector2(1f, 0f), uvMax: new Vector2(0f, 1f)); // flipped sprite
        
        var srcPos  = new Vector2(6 * 64, 2 * 64);  // tile pos in Sheet (6,2)        
        var srcSize = new Vector2(64, 64);          // 64x64 Tile
        var texSize = new Vector2(1024, 1024);      // texture-size
        draw.DrawSprite(new Vector2(500, 50), new Vector2(64, 64), myTextureView, srcPos, srcSize, texSize);
        
        rotation += deltaTime;
        draw.DrawSprite(
            position: new Vector2(100, 550),
            size:     new Vector2(128, 128),
            rotation: rotation,
            pivot:    new Vector2(0.5f, 0.5f), // center
            texture:  myTextureView
        );
        draw.DrawSprite(
            position:       new Vector2(620, 75),
            size:           new Vector2(32, 32),
            rotation:       rotation,
            pivot:          new Vector2(0.5f, 1.0f),        // bottom center
            texture:        myTextureView,
            sourceRectPos:  new Vector2(6 * 64, 2 * 64),    // tile in sheet
            sourceRectSize: new Vector2(64, 64),
            textureSize:    new Vector2(1024, 1024)
        );
    }
    
    public static void DrawShapes(Draw2D draw)
    {
        draw.Line(new Vector2(500, 150), new Vector2(600, 250), thickness: 4f, color: 0xFF0000FF);

        draw.RectangleLines(new Vector2(500, 400), new Vector2(150, 80), thickness: 2f, color: 0x00FF00FF);

        draw.Circle(new Vector2(650, 200), radius: 40f, color: 0x0000FFFF, segments: 32);

        draw.CircleLines(new Vector2(550, 300), radius: 50f, thickness: 3f, color: 0xFFFF00FF, segments: 32);
    }
    
    public static void DrawText(Draw2D draw)
    {
        var textSize = draw.MeasureString("wgpu");
        draw.RectangleLines(new Vector2(700, 50), textSize, 2, Color32.Gray);
        draw.DrawString("wgpu", new Vector2(700, 50), Color32.White);
        
        draw.RectangleLines(new Vector2(850, 50), new Vector2(150, 48), 2, Color32.Gray);
        draw.DrawStringAligned("right", new Vector2(1000, 50), TextAlignment.Right, Color32.Yellow);

        draw.RectangleLines(new Vector2(1050, 50), new Vector2(150, 48), 2, Color32.Gray);
        draw.DrawStringTruncated("truncate me", new Vector2(1050, 50), 150, Color32.Cyan);
        
        draw.RectangleLines(new Vector2(750, 150), new Vector2(200, 200), 2, Color32.Gray);
        var lineCount = draw.DrawStringWrapped("long text with word wrapping", new Vector2(750, 150), 200, Color32.CornflowerBlue);
        Debug.Assert(lineCount == 3);
        
        {
            var btnPos  = new Vector2(1000, 150);
            var btnSize = new Vector2(150,  50);
            draw.Rectangle(btnPos, btnSize, Color32.DarkGray);
            draw.DrawStringInRect("OK", btnPos, btnSize, TextAlignment.Center, VerticalAlignment.Middle, Color32.White);
        } {
            var btnPos  = new Vector2(1000, 250);
            var btnSize = new Vector2(150,  100);
            draw.Rectangle(btnPos, btnSize, Color32.DarkGray);
            draw.DrawStringInRect("OK", btnPos, btnSize, TextAlignment.Center, VerticalAlignment.Middle, Color32.White, scale: 2);
        }
        
        var font = draw.GetDefaultFont();
        Debug.Assert(font.name == "Default Font");
        Debug.Assert((int)font.lineHeight == 47);
    }
}
