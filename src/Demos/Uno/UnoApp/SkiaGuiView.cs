// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.InteropServices;
using Friflo.SmGui.SkiaSharp;
using Microsoft.UI.Xaml.Media.Imaging;
using SkiaSharp;

// ReSharper disable EmptyConstructor
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
    
    public SkiaGuiView() { }
    
    public void CreateGuiApp(CreateGuiApp createGuiApp, IGuiAssets assets)
    {
        if (guiBackend != null) {
            throw new InvalidOperationException("GuiApp already attached");
        }
        guiBackend  = new SkiaSharpGuiBackend(assets);
        batch       = guiBackend.CreateBatch();
        app         = createGuiApp(guiBackend);
        // ValoniaInput.AttachPointerGestures(canvasView, guiBackend);
    }
}
