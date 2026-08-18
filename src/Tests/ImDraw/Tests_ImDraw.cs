using Friflo.WGPU;
using Friflo.WGPU.ImDraw;
using NUnit.Framework;


// ReSharper disable once InconsistentNaming
namespace Tests.ImDraw;

public static class Tests_ImDraw
{
    [Test]
    public static void Tests_ImDraw_GuiStyle()
    {
        var style = new GuiStyle();
        var repeat = 10; // 1_000_000_000   3.95 sec
        for (int n = 0; n < repeat; n++) {
            style.color.ButtonColor = 0xff0000ff;
        }
        Assert.That(style.color.Overrides.Count,                    Is.EqualTo(1));
        Assert.That(style.color.HasOverride(ColorId.ButtonColor),   Is.EqualTo(true));
        
        var color = new GuiColor {
            ButtonDown = 0x110000ff,
            ButtonText = 0x220000ff
        };
        style.color.AddOverrides(color);
        Assert.That(style.color.Overrides.Count, Is.EqualTo(3));
        
        style.color.RemoveOverride(ColorId.ButtonDown);
        Assert.That(style.color.Overrides.Count, Is.EqualTo(2));
        
        style.color.ClearOverrides();
        Assert.That(style.color.Overrides.Count, Is.EqualTo(0));
    }
    
    [Test]
    public static void Tests_ImDraw_DrawGui()
    {
        using var instance    = WgpuInstance.CreateInstance();
        using var adapter     = instance.RequestAdapter(default); // specific backend: new GpuRequestAdapterOptions { backendType = BackendType.D3D12 }
        using var device      = adapter.CreateDevice("test");
        
        using var targetTexture = device.CreateTexture(new GpuTextureDescriptor {
            label  = "Target Texture",
            size   = [1000, 500],
            format = TextureFormat.RGBA8Unorm,
            usage  = TextureUsage.TextureBinding | TextureUsage.CopyDst | TextureUsage.RenderAttachment
        });
        var renderTargetView = targetTexture.CreateView();

        var renderPassDesc = new GpuRenderPassDescriptor {
            colorAttachments = [
                new GpuRenderPassColorAttachment {
                    view        = renderTargetView,
                    loadOp      = LoadOp.Clear,
                    storeOp     = StoreOp.Store,
                    clearValue  = new GpuColor { r = 0.1, g = 0.1, b = 0.1, a = 1.0 }
                }
            ]
        };

        using var context   = device.BeginContext();
        using var batch     = device.CreateBatch2D(TextureFormat.BGRA8Unorm);
        
        using var frame     = context.BeginFrame(renderTargetView, "encoder"u8);
        
        batch.input.NewFrame();
        using var gui = batch.BeginGui(frame, renderPassDesc);
        gui.BeginWindow("Test Window");
        gui.Button("hello");
        gui.Button("test");
        gui.EndWindow();
        gui.draw.Dispose(); // redundant - only for debugging
    }
}