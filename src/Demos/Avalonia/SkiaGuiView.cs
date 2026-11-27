// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Demos.BasicGui;
using Friflo.SmGui.SkiaSharp;
using SkiaSharp;

// ReSharper disable CheckNamespace
namespace Friflo.SmGui.Avalonia;

public sealed class SkiaGuiView : Control, IDisposable
{
    private SkiaSharpGuiBackend?    guiBackend;
    private SkiaSharpBatch?         batch;
    private SmGuiApp?               app;
    
    private SKBitmap?               skBitmap;
    private SKCanvas?               skCanvas;
    private WriteableBitmap?        uiBitmap;
    
    public SkiaGuiView() {
        guiBackend  = new SkiaSharpGuiBackend(new DefaultGuiAssets());
        batch       = guiBackend.CreateBatch();
        app         = new BasicGuiApp(guiBackend);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(0, 0, Bounds.Width, Bounds.Height);
        if (bounds.Width <= 0 || bounds.Height <= 0) {
            return;
        }

        // Runs on the UI/Main thread: update bitmaps if size changed, draw SmGui/Skia, and copy pixels.
        var pixelWidth  = Math.Max(1, (int)Math.Ceiling(bounds.Width));
        var pixelHeight = Math.Max(1, (int)Math.Ceiling(bounds.Height));

        EnsureBitmaps(pixelWidth, pixelHeight);
        
        skCanvas?.Clear(SKColors.Transparent);

        if (guiBackend != null && batch != null) {
            guiBackend.NewFrame();
            app?.RenderApp(batch, pixelWidth, pixelHeight);
            batch.DrawCommandList(skCanvas, 0, 0, pixelWidth, pixelHeight);
            // AvaloniaCursor.SetMouseCursor(guiBackend, this);
        }
        // using var paint = new SKPaint { Color = SKColors.DodgerBlue, IsAntialias = true };
        // canvas.DrawCircle((float)(bounds.Width * 0.5), (float)(bounds.Height * 0.5), 40f, paint);

        CopyPixelsToWriteableBitmap(skBitmap!, uiBitmap!);
        context.DrawImage(uiBitmap!, new Rect(0, 0, uiBitmap!.PixelSize.Width, uiBitmap.PixelSize.Height), bounds);
    }

    public void Dispose()
    {
        uiBitmap?.Dispose();
        uiBitmap = null;
        skCanvas?.Dispose();
        skCanvas = null;
        skBitmap?.Dispose();
        skBitmap = null;
    }

    private void EnsureBitmaps(int width, int height)
    {
        if (skBitmap is not null && skBitmap.Width == width && skBitmap.Height == height && skCanvas is not null && uiBitmap is not null) {
            return;
        }

        uiBitmap?.Dispose();
        skCanvas?.Dispose();
        skBitmap?.Dispose();

        skBitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        skCanvas = new SKCanvas(skBitmap);
        uiBitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
    }

    private static void CopyPixelsToWriteableBitmap(SKBitmap source, WriteableBitmap target)
    {
        // Copy raw BGRA8888 pixels via Marshal.Copy without unsafe pointers.
        using var locked = target.Lock();
        var srcPtr = source.GetPixels();
        var dstPtr = locked.Address;
        var srcRowBytes = source.RowBytes;
        var dstRowBytes = locked.RowBytes;
        var height = source.Height;

        if (srcRowBytes == dstRowBytes) {
            var totalBytes = srcRowBytes * height;
            var buffer = new byte[totalBytes];
            Marshal.Copy(srcPtr, buffer, 0, totalBytes);
            Marshal.Copy(buffer, 0, dstPtr, totalBytes);
            return;
        }

        var rowBuffer = new byte[Math.Min(srcRowBytes, dstRowBytes)];
        for (var y = 0; y < height; y++) {
            Marshal.Copy(srcPtr + y * srcRowBytes, rowBuffer, 0, rowBuffer.Length);
            Marshal.Copy(rowBuffer, 0, dstPtr + y * dstRowBytes, rowBuffer.Length);
        }
    }
}