using System;
using System.Globalization;
using System.IO;
using System.Text;
using Friflo.TmGui;
using Friflo.WGPU;
using Friflo.WGPU.TmGui;
using NUnit.Framework;
using StbImageWriteSharp;

// ReSharper disable SuggestVarOrType_Elsewhere
// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable UnusedMember.Local
// ReSharper disable once InconsistentNaming
namespace Tests.TmDraw;

public static class Tests_TmDraw
{
    [Test]
    public static void Tests_TmDraw_GuiStyle()
    {
        var style = new GuiStyle();
        var repeat = 10; // 1_000_000_000   3.95 sec
        for (int n = 0; n < repeat; n++) {
            style.colors.ButtonColor = 0xff0000ff;
        }
        Assert.That(style.colors.Overrides.Count,                    Is.EqualTo(1));
        Assert.That(style.colors.HasOverride(ColorId.ButtonColor),   Is.EqualTo(true));
        
        var color = new GuiColors {
            ButtonDown = 0x110000ff,
            ButtonText = 0x220000ff
        };
        style.colors.AddOverrides(color);
        Assert.That(style.colors.Overrides.Count, Is.EqualTo(3));
        
        style.colors.RemoveOverride(ColorId.ButtonDown);
        Assert.That(style.colors.Overrides.Count, Is.EqualTo(2));
        
        style.colors.ClearOverrides();
        Assert.That(style.colors.Overrides.Count, Is.EqualTo(0));
    }
    
    private static void Ensure_public_API(Gui gui)
    {
        StringBuilder sb = gui.widget.StringBuilder();
        ReadOnlySpan<char> _ = sb.Span(); // ensure StringBuilderExtensions is public
    }
    
    [Test]
    public static void Tests_TmDraw_StringBuilderExtensions()
    {
        {
            var sb = new StringBuilder();
            _ = sb.AppendFloat(123.456f, "F1", CultureInfo.InvariantCulture);
            Assert.That(sb.ToString(), Is.EqualTo("123.5"));
        } {
            var sb = new StringBuilder();
            _ = sb.AppendDouble(123.456d, "F1", CultureInfo.InvariantCulture);
            Assert.That(sb.ToString(), Is.EqualTo("123.5"));
        }
    }
    
    
    [Test]
    [Platform(Exclude = "MacOsX", Reason = "Hangs occasionally on macOS (Metal issue)")]
    public static void Tests_TmDraw_DrawGui()
    {
        // TODO  hangs occasionally in macOS. Debugging it when in fitting Mood

        using var instance    = WgpuInstance.CreateInstance();
        
        TmDraw_DrawGui_Offscreen(instance);
    
        var handles = instance.GenerateHandles();
        Assert.IsTrue(handles.IsActiveZero());
    }
    
    private static void TmDraw_DrawGui_Offscreen(WgpuInstance instance)
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
        using var guiBackend= new WgpuGuiBackend(device);
        using var batch     = guiBackend.CreateBatch(TextureFormat.RGBA8Unorm);
        using var target    = context.BeginRenderTarget(renderTargetView, "Texture-Encoder"u8);
        
        guiBackend.NewFrame(); // not necessary
        var gui = batch.BeginGui(target.TargetSize.width, target.TargetSize.height);
        var windowScope = gui.BeginWindow("Test Window");
        gui.Button("hello");
        gui.Button("test");
        gui.EndWindow(windowScope);
        
        _ = gui.widget.Colors.ButtonColor;  // Ensures Colors is available
        _ = gui.LineHeight;                 // Ensures LineHeight is available
        
        batch.DrawCommandList(target, renderPassDesc);
        
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