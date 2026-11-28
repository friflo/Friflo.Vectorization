using SkiaSharp;

namespace WinFormsApp;

public partial class Form1 : Form
{
    private SKBitmap? bitmap;

    public Form1() {
        // Enable double buffering to reduce flicker when painting custom bitmaps
        DoubleBuffered = true;
        ClientSize = new Size(800, 450);
    }

    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);

        int width = Math.Max(1, ClientSize.Width);
        int height = Math.Max(1, ClientSize.Height);

        if (bitmap is null || bitmap.Width != width || bitmap.Height != height) {
            bitmap?.Dispose();
            bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        }

        // Render onto SKCanvas backed by SKBitmap
        using (var canvas = new SKCanvas(bitmap)) {
            canvas.Clear(SKColors.White);

            using var paint = new SKPaint {
                Color = SKColors.DodgerBlue,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };

            canvas.DrawCircle(width / 2f, height / 2f, Math.Min(width, height) * 0.25f, paint);
        }

        // Copy BGRA pixel buffer into a WinForms Bitmap and draw it
        using var gdiBitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        var data = gdiBitmap.LockBits(
            new Rectangle(0, 0, width, height),
            System.Drawing.Imaging.ImageLockMode.WriteOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppPArgb);


        int byteLength = width * height * 4;
        unsafe {
            var src = new ReadOnlySpan<byte>((void*)bitmap.GetPixels(), byteLength);
            var dst = new Span<byte>((void*)data.Scan0, byteLength);
            src.CopyTo(dst);
        }
        
        gdiBitmap.UnlockBits(data);

        e.Graphics.DrawImageUnscaled(gdiBitmap, 0, 0);
    }
    
    
    public void Dispose() {

        bitmap?.Dispose();

    }
}