// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.Avalonia;

public sealed class SkiaGuiView : Control
{
    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(0, 0, Bounds.Width, Bounds.Height);
        if (bounds.Width <= 0 || bounds.Height <= 0) {
            return;
        }

        // Delegate actual SkiaSharp drawing to a custom draw operation.
        context.Custom(new SkiaDrawOperation(bounds, DrawCanvas));
    }

    private void DrawCanvas(SKCanvas canvas, Rect bounds)
    {
        // Draw directly on SkiaSharp's SKCanvas.
        using var paint = new SKPaint {
            Color = SKColors.DodgerBlue,
            IsAntialias = true
        };
        canvas.DrawCircle((float)(bounds.Width * 0.5), (float)(bounds.Height * 0.5), 40f, paint);
    }

    private sealed class SkiaDrawOperation : ICustomDrawOperation
    {
        private readonly Action<SKCanvas, Rect> draw;

        public SkiaDrawOperation(Rect bounds, Action<SKCanvas, Rect> draw)
        {
            Bounds = bounds;
            this.draw = draw;
        }

        public Rect Bounds { get; }

        public bool HitTest(Point p) => Bounds.Contains(p);

        public bool Equals(ICustomDrawOperation? other) => false;

        public void Dispose() { }

        public void Render(ImmediateDrawingContext context)
        {
            // Lease the underlying SkiaSharp canvas for this draw pass.
            var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature is null) {
                return;
            }

            using var lease = leaseFeature.Lease();
            draw(lease.SkCanvas, Bounds);
        }
    }
}