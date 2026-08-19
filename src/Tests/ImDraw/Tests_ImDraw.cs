using System;
using System.IO;
using System.Runtime.InteropServices;
using Friflo.WGPU;
using Friflo.WGPU.ImDraw;
using NUnit.Framework;
using StbImageWriteSharp;


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
    [Platform(Exclude = "MacOsX", Reason = "Hangs occasionally on macOS (Metal issue)")]
    public static void Tests_ImDraw_DrawGui()
    {
        // TODO  hangs occasionally in macOS. Debugging it when in fitting Mood

        using var instance    = WgpuInstance.CreateInstance();
        
        ImDraw_DrawGui_Offscreen(instance);
    
        var handles = instance.GenerateHandles();
        Assert.IsTrue(handles.IsActiveZero());
    }
    
    private static void ImDraw_DrawGui_Offscreen(WgpuInstance instance)
    {
        using var adapter     = instance.RequestAdapter(default); // specific backend: new GpuRequestAdapterOptions { backendType = BackendType.D3D12 }
        using var device      = adapter.CreateDevice("test");
        
        var width  = 500;
        var height = 300;
        using var targetTexture = device.CreateTexture(new GpuTextureDescriptor {
            label  = "Target Texture",
            size   = [width, height],
            format = TextureFormat.RGBA8Unorm,
            usage  = TextureUsage.CopyDst | TextureUsage.CopySrc | TextureUsage.RenderAttachment
        });
        var renderTargetView = targetTexture.CreateView(); // is owned by targetTexture

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
        using var batch     = device.CreateBatch2D(TextureFormat.RGBA8Unorm);
        using var target    = context.BeginRenderTarget(renderTargetView, "Texture-Encoder"u8);
        
        batch.input.NewFrame(); // not necessary
        using (var gui = batch.BeginGui(target, renderPassDesc)) {
            gui.BeginWindow("Test Window");
            gui.Button("hello");
            gui.Button("test");
            gui.EndWindow();
            gui.draw.Dispose(); // redundant - only for debugging
        }
        
        var targetMemory = new byte[width * height * 4];
        targetTexture.Read(context, width, height, 4, new Memory<byte>(targetMemory));
        
        context.Queue.ReadBuffers(); // <= Submit() & Wait
        
        var filePath = Path.GetFullPath("test_output.png");
        using (var stream = File.Create(filePath)) {
            var writer = new ImageWriter();
            writer.WritePng(targetMemory, width, height, ColorComponents.RedGreenBlueAlpha, stream);
        }
    }
}