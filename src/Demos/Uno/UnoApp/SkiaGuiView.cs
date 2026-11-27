using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using SkiaSharp;

// ReSharper disable CheckNamespace
namespace Friflo.SmGui.Uno;


public sealed class SkiaGuiView : Control, IDisposable
{
    private Image?                  uiImage;
    private SKBitmap?               skBitmap;
    private SKCanvas?               skCanvas;
    private Microsoft.UI.Xaml.Media.Imaging.WriteableBitmap? uiBitmap;
    private byte[]?                 pixelBuffer;
    private int                     pixelWidth;
    private int                     pixelHeight;

    public SkiaGuiView()
    {
        DefaultStyleKey = typeof(SkiaGuiView);
        SizeChanged += OnSizeChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        // Retrieve the template image element used to display the rendered bitmap
        uiImage = GetTemplateChild("PART_Image") as Image;
        if (uiImage != null && uiBitmap != null) {
            uiImage.Source = uiBitmap;
        }
        RenderCircle();
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

        uiBitmap = new Microsoft.UI.Xaml.Media.Imaging.WriteableBitmap(pixelWidth, pixelHeight);
        if (uiImage != null) {
            uiImage.Source = uiBitmap;
        }
    }

    private void RenderCircle()
    {
        if (skCanvas == null || skBitmap == null || uiBitmap == null || pixelBuffer == null) {
            return;
        }

        skCanvas.Clear(SKColors.Red);

        float cx = pixelWidth * 0.5f;
        float cy = pixelHeight * 0.5f;
        float radius = Math.Min(cx, cy) * 0.6f;

        using var paint = new SKPaint {
            Color = SKColors.DodgerBlue,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };
        skCanvas.DrawCircle(cx, cy, radius, paint);

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
