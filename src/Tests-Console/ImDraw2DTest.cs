using System.Diagnostics;
using System.Numerics;
using Friflo.ImGui;
using Friflo.WGPU;
using Friflo.WGPU.ImGui;


// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InconsistentNaming
// ReSharper disable ConvertToPrimaryConstructor
namespace TestConsole;

public class ImRenderer : IRenderer
{
    private readonly    WgpuBatch               batch;
    private readonly    GpuTexture              myTexture;
    private readonly    ImTexture               myTextureView;
    private readonly    GpuRenderPassDescriptor renderPassDescriptor    = new () { colorAttachments = [ default ] };
    private readonly    Stopwatch               stopwatch               = Stopwatch.StartNew();
    private             float                   lastTime;
    private             float                   rotation;
    private readonly    PerfLog                 perfLog                 = new();
    
    public void OnShutdown() {
        myTexture.Dispose();
        batch.Dispose();
    }
    
    public ImRenderer(WgpuHost wgpuHost)
    {
        var guiBackend = wgpuHost.CreateGuiBackend();
        batch = guiBackend.CreateBatch2D(guiBackend, wgpuHost.SwapChainFormat);
        
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
            clearValue  = new GpuColor(0.1, 0.1, 0.1, 1)
        };
    }
    
    public void OnFrame(in RenderTarget target)
    {
        perfLog.Trace(5000);
        var currentTime = (float)stopwatch.Elapsed.TotalSeconds;
        var deltaTime   = currentTime - lastTime;
        lastTime        = currentTime;
        
        var draw = batch.BeginDraw2D(target.Width,  target.Height);
        
        // draw.SetBlendState(BlendState.Additive);
        // draw.SetFilterMode(FilterMode.Nearest); // Demonstrates pixel jittering (nearest) vs. smooth interpolation (linear)
        using (draw.PushTransform(CreateAnimatedTransform(target.TargetSize, currentTime))) {
            DrawShapes(draw, target.TargetSize.width, target.TargetSize.height);
            DrawSprites(draw, deltaTime);
        }
        DrawText(draw);
        
        batch.DrawCommandList(target, renderPassDescriptor);
    }
    
    private static Matrix4x4 CreateAnimatedTransform(GpuExtent3D size, float time)
    {
        var center = new Vector2(size.width * 0.5f, size.height * 0.5f);
        var scale  = 1.0f + MathF.Sin(time * 2f) * 0.05f; // Sanftes Atmen
        var angle  = MathF.Sin(time) * 0.03f;             // Leichtes Neigen

        return Matrix4x4.CreateTranslation(-center.X, -center.Y, 0f)
             * Matrix4x4.CreateRotationZ(angle)
             * Matrix4x4.CreateScale(scale, scale, 1f)
             * Matrix4x4.CreateTranslation(center.X, center.Y, 0f);
    }
    
    public void DrawSprites(ImDraw draw, float deltaTime)
    {
        // --- sprites
        draw.DrawSprite(myTextureView, new Vector2( 50, 150), new Vector2(256, 256));
        draw.DrawSprite(myTextureView, new Vector2(200, 150), new Vector2(256, 256), new Vector2(1f, 0f), new Vector2(0f, 1f)); // flipped sprite
        
        rotation += deltaTime;
        draw.DrawSpriteRotated(// center
            texture: myTextureView, position: new Vector2(100, 550), size: new Vector2(128, 128), rotation: rotation, pivot: new Vector2(0.5f, 0.5f));
        draw.DrawSpriteRegionRotated(// bottom center
            texture: myTextureView,
            position: new Vector2(275, 475),    // tile in sheet
            size: new Vector2(32, 32),
            rotation: rotation, pivot: new Vector2(0.5f, 1.0f), sourceRectPos: new Vector2(6 * 64, 2 * 64), sourceRectSize: new Vector2(64, 64), textureSize: new Vector2(1024, 1024));
        
        var srcPos  = new Vector2(3 * 64, 3 * 64);  // tile pos in Sheet (6,2)        
        var srcSize = new Vector2(64, 64);          // 64x64 Tile
        var texSize = new Vector2(1024, 1024);      // texture-size
        draw.DrawSpriteRegion(myTextureView, new Vector2(350, 450), new Vector2(64, 64), srcPos, srcSize, texSize);
        
        var borders = new Vector4(8, 8, 8, 8);
        draw.Draw9SliceTiled(texture: myTextureView, 
            position: new Vector2(250, 550), 
            size: new Vector2(200, 100), 
            sourceRectPos: srcPos, 
            sourceRectSize: srcSize, textureSize: texSize, borderThickness: borders);
    }
    
    public static void DrawShapes(ImDraw draw, int width, int height)
    {
        draw.FillRect(new Vector2(1, 1), new Vector2(99, 99), 0xFFFFFFFF);
        draw.FillRect(new Vector2(width - 100, height - 100), new Vector2(99, 99), 0xFFFFFFFF);
        
        draw.FillRect(new Vector2(150, 50), new Vector2(50, 50), Color32.Red);
        draw.FillRect(new Vector2(250, 50), new Vector2(50, 50), Color32.Green);
        draw.FillRect(new Vector2(350, 50), new Vector2(50, 50), Color32.Blue);
        
        draw.FillRectGradient(new Vector2(450, 50),new Vector2(50, 50), topLeft: Color32.Red, topRight: Color32.White, bottomRight: Color32.Red, bottomLeft: Color32.Purple);
        draw.FillRectGradientVertical(new Vector2(550, 50), new Vector2(50, 50), top: Color32.Red, bottom: Color32.Purple);
        
        draw.StrokeLine(new Vector2(500, 150), new Vector2(600, 250), thickness: 4f, color: 0xFF0000FF);

        draw.FillCircle(new Vector2(650, 200), radius: 40f, color: 0x0000FFFF, segments: 32);

        draw.StrokeCircle(new Vector2(550, 300), radius: 50f, thickness: 3f, color: 0xFFFF00FF, segments: 32);
        
        draw.StrokeRect(new Vector2(500, 400), new Vector2(50, 80), thickness: 2f, color: 0x00FF00FF);
        
        draw.FillTriangle(new Vector2(600, 450), new Vector2(650, 420), new Vector2(650, 480), color: 0x0000FFFF);
    }
    
    public static void DrawText(ImDraw draw)
    {
        var textSize = draw.MeasureText("wgpu");
        draw.StrokeRect(new Vector2(700, 50), textSize, 2, Color32.Gray);
        draw.DrawText("wgpu", new Vector2(700, 50), Color32.White);
        
        draw.StrokeRect(new Vector2(850, 50), new Vector2(150, 48), 2, Color32.Gray);
        draw.DrawTextAligned("right", new Vector2(1000, 50), TextAlignment.Right, Color32.Yellow);

        draw.StrokeRect(new Vector2(1050, 50), new Vector2(150, 48), 2, Color32.Gray);
        draw.DrawTextTruncated("truncate me", new Vector2(1050, 50), 150, Color32.Cyan);
        
        draw.StrokeRect(new Vector2(750, 150), new Vector2(200, 220), 2, Color32.Gray);
            using (draw.PushScissor(new Vector2(750, 150), new Vector2(200, 220))) {
            var lineCount = draw.DrawTextWrapped("Clipped long text with word wrapping. More text that need to be clipped.", new Vector2(750, 150), 200, Color32.CornflowerBlue);
            Debug.Assert(lineCount == 8);
        }
        {
            var btnPos  = new Vector2(1000, 150);
            var btnSize = new Vector2(150,  50);
            draw.FillRectRounded(btnPos, btnSize, 14, Color32.DarkGray);
            draw.DrawTextInRect("OK", btnPos, btnSize, TextAlignment.Center, VerticalAlignment.Middle, Color32.White);
        } {
            var btnPos  = new Vector2(1000, 250);
            var btnSize = new Vector2(150,  100);
            draw.FillRectRounded(btnPos, btnSize, 14, Color32.DarkGray);
            draw.DrawTextInRect("OK", btnPos, btnSize, TextAlignment.Center, VerticalAlignment.Middle, Color32.White, scale: 2);
        }
        
        var font = draw.DefaultFont;
        Debug.Assert(font.name == "Default Font");
        Debug.Assert((int)font.lineHeight == 47);
        Debug.Assert(font.glyphs.Count == 191);
    }
}
