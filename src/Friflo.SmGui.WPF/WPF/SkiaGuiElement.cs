
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Friflo.SmGui.SkiaSharp;
using SkiaSharp;

// ReSharper disable CheckNamespace
namespace Friflo.SmGui.WPF;


public sealed class SkiaGuiElement : FrameworkElement, IDisposable
{
    private SkiaSharpGuiBackend?    guiBackend;
    private SkiaSharpBatch?         batch;
    private SmGuiApp?               app;

    private SKBitmap?               skBitmap;
    private SKCanvas?               skCanvas;
    private WriteableBitmap?        uiBitmap;

    public void CreateGuiApp(CreateGuiApp createGuiApp, IGuiAssets assets)
    {
        if (guiBackend != null) {
            throw new InvalidOperationException("GuiApp already attached");
        }
        guiBackend  = new SkiaSharpGuiBackend(assets);
        batch       = guiBackend.CreateBatch();
        app         = createGuiApp(guiBackend);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        if (ActualWidth <= 0 || ActualHeight <= 0) {
            return;
        }

        int pixelWidth  = Math.Max(1, (int)Math.Ceiling(ActualWidth));
        int pixelHeight = Math.Max(1, (int)Math.Ceiling(ActualHeight));

        EnsureBitmaps(pixelWidth, pixelHeight);

        var color = app?.ClearValue ?? Color32.Transparent;
        skCanvas?.Clear(color.AsSkColor());

        if (guiBackend != null && batch != null) {
            guiBackend.NewFrame();
            app?.RenderApp(batch, pixelWidth, pixelHeight);
            batch.DrawCommandList(skCanvas, 0, 0, pixelWidth, pixelHeight);
        }

        // Copy BGRA8888 pixels into WPF WriteableBitmap
        uiBitmap!.Lock();
        try {
            int byteLength = pixelWidth * pixelHeight * 4;
            unsafe {
                var src = new ReadOnlySpan<byte>((void*)skBitmap!.GetPixels(), byteLength);
                var dst = new Span<byte>((void*)uiBitmap.BackBuffer, byteLength);
                src.CopyTo(dst);
            }
            uiBitmap.AddDirtyRect(new Int32Rect(0, 0, pixelWidth, pixelHeight));
        }
        finally {
            uiBitmap.Unlock();
        }

        drawingContext.DrawImage(uiBitmap, new Rect(0, 0, ActualWidth, ActualHeight));
    }

    public void Dispose()
    {
        uiBitmap = null;
        skCanvas?.Dispose();
        skCanvas = null;
        skBitmap?.Dispose();
        skBitmap = null;
    }

    private void EnsureBitmaps(int width, int height)
    {
        // Recreate backing bitmaps only when control size changes
        if (skBitmap is not null && skBitmap.Width == width && skBitmap.Height == height && skCanvas is not null && uiBitmap is not null) {
            return;
        }

        skCanvas?.Dispose();
        skBitmap?.Dispose();

        skBitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        skCanvas = new SKCanvas(skBitmap);
        uiBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
    }
}