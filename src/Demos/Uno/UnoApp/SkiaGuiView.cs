using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Friflo.SmGui.SkiaSharp;
using Microsoft.UI.Xaml.Media.Imaging;
using SkiaSharp;

// ReSharper disable CheckNamespace
namespace Friflo.SmGui.Uno;


public sealed class SkiaGuiView : ContentControl, IDisposable
{
    private SkiaSharpGuiBackend?    guiBackend;
    private SkiaSharpBatch?         batch;
    private SmGuiApp?               app;
    //
    private readonly Image          uiImage = new() { Stretch = Stretch.Fill };
    private SKBitmap?               skBitmap;
    private SKCanvas?               skCanvas;
    private WriteableBitmap?        uiBitmap;
    private byte[]?                 pixelBuffer;
    private int                     pixelWidth;
    private int                     pixelHeight;

    public SkiaGuiView()
    {
        // Attach Image directly as content instead of using a ControlTemplate
        Content = uiImage;
        SizeChanged += OnSizeChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }
    
    public void CreateGuiApp(CreateGuiApp createGuiApp, IGuiAssets assets)
    {
        if (guiBackend != null) {
            throw new InvalidOperationException("GuiApp already attached");
        }
        guiBackend  = new SkiaSharpGuiBackend(assets);
        batch       = guiBackend.CreateBatch();
        app         = createGuiApp(guiBackend);
        // UnoInput.AttachPointerGestures(canvasView, guiBackend);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        CompositionTarget.Rendering += OnRendering;
        RenderCircle();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        CompositionTarget.Rendering -= OnRendering;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        EnsureSurfaces((int)Math.Max(1, e.NewSize.Width), (int)Math.Max(1, e.NewSize.Height));
        RenderCircle();
    }

    private void OnRendering(object? sender, object e)
    {
        RenderCircle();
    }

    private void EnsureSurfaces(int width, int height)
    {
        if (width == pixelWidth && height == pixelHeight && skBitmap != null && uiBitmap != null) {
            return;
        }

        pixelWidth = width;
        pixelHeight = height;

        skCanvas?.Dispose();
        skBitmap?.Dispose();

        // Allocate reusable SKBitmap and SKCanvas on the UI thread
        skBitmap = new SKBitmap(pixelWidth, pixelHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
        skCanvas = new SKCanvas(skBitmap);
        pixelBuffer = new byte[pixelWidth * pixelHeight * 4];

        uiBitmap = new WriteableBitmap(pixelWidth, pixelHeight);
        uiImage.Source = uiBitmap;
    }

    private void RenderCircle()
    {
        if (skCanvas == null || skBitmap == null || uiBitmap == null || pixelBuffer == null) {
            return;
        }
        skCanvas.Clear(SKColors.Transparent);
        
        if (guiBackend != null && batch != null) {
            guiBackend.NewFrame();
            app?.RenderApp(batch, pixelWidth, pixelHeight);
            batch.DrawCommandList(skCanvas, 0, 0, pixelWidth, pixelHeight);
            // UnoCursor.SetMouseCursor(guiBackend, this);
        }
        
        // using var paint = new SKPaint { Color = SKColors.DodgerBlue, IsAntialias = true, Style = SKPaintStyle.Fill };
        // skCanvas.DrawCircle((float)(pixelWidth * 0.5), (float)(pixelHeight * 0.5), 40f, paint);

        // Copy SKBitmap pixels into the reusable buffer and blit to WriteableBitmap
        IntPtr srcPtr = skBitmap.GetPixels();
        Marshal.Copy(srcPtr, pixelBuffer, 0, pixelBuffer.Length);

        using Stream dstStream = uiBitmap.PixelBuffer.AsStream();
        dstStream.Seek(0, SeekOrigin.Begin);
        dstStream.Write(pixelBuffer, 0, pixelBuffer.Length);
        uiBitmap.Invalidate();
    }

    public void Dispose()
    {
        SizeChanged -= OnSizeChanged;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        CompositionTarget.Rendering -= OnRendering;

        skCanvas?.Dispose();
        skBitmap?.Dispose();
        pixelBuffer = null;
        uiBitmap = null;
    }
}
