// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Demos.BasicGui;
using Friflo.SmGui.SkiaSharp;
using SkiaSharp;

// ReSharper disable CheckNamespace
namespace Friflo.SmGui.Avalonia;

public sealed class SkiaGuiView : Control, ICustomDrawOperation
{
    private             SkiaSharpGuiBackend?    guiBackend;
    private             SkiaSharpBatch?         batch;
    private             SmGuiApp?               app;
    
    public SkiaGuiView()
    {
        guiBackend  = new SkiaSharpGuiBackend(new DefaultGuiAssets());
        batch       = guiBackend.CreateBatch();
        app         = new BasicGuiApp(guiBackend);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(0, 0, base.Bounds.Width, base.Bounds.Height);
        if (bounds.Width <= 0 || bounds.Height <= 0) {
            return;
        }
        Bounds = bounds;
        context.Custom(this);
    }
    
    private static void DrawCanvas(SKCanvas canvas, Rect bounds)
    {
        // Draw directly on SkiaSharp's SKCanvas.
        using var paint = new SKPaint {
            Color = SKColors.DodgerBlue,
            IsAntialias = true
        };
        canvas.DrawCircle((float)(bounds.Width * 0.5), (float)(bounds.Height * 0.5), 40f, paint);
    }

    // --- ICustomDrawOperation
    Rect ICustomDrawOperation.Bounds => base.Bounds;

    public bool HitTest(Point p) => Bounds.Contains(p);

    public bool Equals(ICustomDrawOperation? other) => ReferenceEquals(this, other);

    public void Dispose() { }

    public void Render(ImmediateDrawingContext context)
    {
        // Lease the SkiaSharp canvas directly for this draw pass.
        var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
        if (leaseFeature is null) {
            return;
        }

        using var lease = leaseFeature.Lease();
        DrawCanvas(lease.SkCanvas, Bounds);
    }
}