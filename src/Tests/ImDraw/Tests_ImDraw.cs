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
    
    // [Test]
    public static void Tests_ImDraw_DrawGui()
    {
        using var instance    = WgpuInstance.CreateInstance();
        using var adapter     = instance.RequestAdapter(default); // specific backend: new GpuRequestAdapterOptions { backendType = BackendType.D3D12 }
        using var device      = adapter.CreateDevice("test");

        using var context = device.BeginContext();
        using var batch = device.CreateBatch2D(TextureFormat.BGRA8Unorm);
        
        using var frame = context.BeginFrame(default, new Size2D(100, 100), "encode"u8);
        
        using var gui = batch.BeginGui(frame, default);
        gui.BeginWindow("Test Window");
        gui.EndWindow();
    }
}