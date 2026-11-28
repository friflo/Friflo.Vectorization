using SkiaSharp;

// ReSharper disable CheckNamespace
namespace Friflo.SmGui.WinForms;

public class SkiaGuiControl : Control
{
    private SKBitmap?   bitmap;
    private Bitmap?     gdiBitmap;

    public SkiaGuiControl() {
        // Reduce flicker during repeated OnPaint repaints
        DoubleBuffered = true;
    }

    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);

        int width = Math.Max(1, ClientSize.Width);
        int height = Math.Max(1, ClientSize.Height);

        EnsureBuffers(width, height);

        // Render onto SKCanvas backed by SKBitmap
        using (var canvas = new SKCanvas(bitmap!)) {
            canvas.Clear(SKColors.White);

            using var paint = new SKPaint {
                Color = SKColors.DodgerBlue,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };

            canvas.DrawCircle(width / 2f, height / 2f, Math.Min(width, height) * 0.25f, paint);
        }

        // Copy BGRA pixel buffer in a single Span.CopyTo call
        var bmpData = gdiBitmap!.LockBits(
            new Rectangle(0, 0, width, height),
            System.Drawing.Imaging.ImageLockMode.WriteOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppPArgb);

        try {
            int byteLength = width * height * 4;
            unsafe {
                var src = new ReadOnlySpan<byte>((void*)bitmap!.GetPixels(), byteLength);
                var dst = new Span<byte>((void*)bmpData.Scan0, byteLength);
                src.CopyTo(dst);
            }
        }
        finally {
            gdiBitmap.UnlockBits(bmpData);
        }

        e.Graphics.DrawImageUnscaled(gdiBitmap, 0, 0);
    }

    private void EnsureBuffers(int width, int height) {
        // Recreate backing bitmaps only when control size changes
        if (bitmap is not null && bitmap.Width == width && bitmap.Height == height && gdiBitmap is not null) {
            return;
        }

        bitmap?.Dispose();
        gdiBitmap?.Dispose();

        bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        gdiBitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
    }

    protected override void Dispose(bool disposing) {
        if (disposing) {
            bitmap?.Dispose();
            gdiBitmap?.Dispose();
        }
        base.Dispose(disposing);
    }
}