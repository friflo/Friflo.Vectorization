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
    private readonly    Batcher2D               batcher;
    private readonly    GpuTexture              myTexture;
    private readonly    GpuTextureView          myTextureView;
    private readonly    GpuRenderPassDescriptor renderPassDescriptor    = new () { colorAttachments = [ default ] };
    private readonly    PerfLog                 perfLog                 = new();
    
    public void OnShutdown() {
        myTexture.Dispose();
        batcher.Dispose();
    }
    
    public ImRenderer(Wgpu wgpu)
    {
        var device = wgpu.Device;
        batcher = new Batcher2D((WgpuDevice)device, wgpu.Config.Descriptor);
        
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
        
        using var draw = batcher.BeginDraw2D(frame, renderPassDescriptor);
        
        draw.Rectangle(new Vector2(1, 1), new Vector2(99, 99), 0xFFFFFFFF);
        draw.Rectangle(new Vector2(frame.Width - 100, frame.Height - 100), new Vector2(99, 99), 0xFFFFFFFF);
        
        draw.Rectangle(new Vector2(200, 50), new Vector2(100, 100), Color32.Red);
        draw.Rectangle(new Vector2(400, 50), new Vector2(100, 100), Color32.Green);
        draw.Rectangle(new Vector2(600, 50), new Vector2(100, 100), Color32.Blue);
        
        // --- sprites
        draw.DrawSprite(new Vector2( 50, 200), new Vector2(256, 256), myTextureView);
        draw.DrawSprite(new Vector2(350, 200), new Vector2(256, 256), myTextureView, uvMin: new Vector2(1f, 0f), uvMax: new Vector2(0f, 1f)); // flipped sprite
        
        var srcPos  = new Vector2(6 * 64, 2 * 64);  // tile pos in Sheet (6,2)        
        var srcSize = new Vector2(64, 64);          // 64x64 Tile
        var texSize = new Vector2(1024, 1024);      // texture-size
        draw.DrawSprite(new Vector2(650, 200), new Vector2(32, 32), myTextureView, srcPos, srcSize, texSize);
        
        draw.Flush(); // redundant - kept for debugging
    }
}
