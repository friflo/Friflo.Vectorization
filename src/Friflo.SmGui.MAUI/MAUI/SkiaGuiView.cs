using System;
using Friflo.SmGui.SkiaSharp;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;


// ReSharper disable RedundantExtendsListEntry
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.MAUI;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
internal sealed class PublicAttribute : Attribute;

public class SkiaGuiView : ContentView
{
    private readonly    SKCanvasView            canvasView;
    private             SkiaSharpGuiBackend?    guiBackend;
    private             SkiaSharpBatch?         batch;
    private             SmGuiApp?               app;
    private             IDispatcherTimer?       renderTimer;
    
    public SkiaGuiView()
    {
        canvasView = new SKCanvasView();
        canvasView.PaintSurface += OnCanvasViewPaintSurface;
        Content = canvasView;

        HandlerChanged  += OnHandlerChanged;
        Unloaded        += OnUnloaded;
    }
    
    public void CreateGuiApp(CreateGuiApp createGuiApp, IGuiAssets assets)
    {
        if (guiBackend != null) {
            throw new InvalidOperationException("GuiApp already attached");
        }
        guiBackend  = new SkiaSharpGuiBackend(assets);
        batch       = guiBackend.CreateBatch();
        app         = createGuiApp(guiBackend);
        MauiInput.AttachPointerGestures(canvasView, guiBackend);
    }
    
    internal bool RaiseKey(HostKeyEventArgs e)
    {
        // Console.WriteLine($"Key: {e.Key} Character: `{e.Character}` {e.IsDown} ");
        var keyEvent = new KeyEvent { code = e.Key, isDown = e.IsDown };
        guiBackend?.AddEvent(new SmEvent(e.IsDown ? SmEventType.KeyDown : SmEventType.KeyUp, keyEvent));
        return e.Handled;
    }

    private void StartRenderLoop(double fps)
    {
        var dispatcher = Dispatcher ?? Application.Current?.Dispatcher;
        if (dispatcher is null)
            return;

        renderTimer ??= dispatcher.CreateTimer();
        renderTimer.Interval = TimeSpan.FromSeconds(1.0 / fps);
        renderTimer.Tick -= OnRenderTimerTick;
        renderTimer.Tick += OnRenderTimerTick;

        if (!renderTimer.IsRunning)
            renderTimer.Start();
    }

    private void StopRenderLoop()
    {
        if (renderTimer is not null && renderTimer.IsRunning) {
            renderTimer.Stop();
        }
    }

    private void OnRenderTimerTick(object? sender, EventArgs e)
    {
        canvasView.InvalidateSurface();
    }

    private void OnHandlerChanged(object? sender, EventArgs e)
    {
        if (Handler is not null) {
            StartRenderLoop(200.0);
        } else {
            StopRenderLoop();
        }
    }

    private void OnUnloaded(object? sender, EventArgs e) {
        StopRenderLoop();
        batch?.Destroy();
        batch = null;
    }

    private void OnCanvasViewPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var color = app?.ClearValue ?? Color32.Transparent;
        canvas.Clear(color.AsSkColor());
        
        var width = e.Info.Width;
        var height = e.Info.Height;

        if (width <= 0 || height <= 0)
            return;

        if (guiBackend != null && batch != null) {
            guiBackend.NewFrame();
            app?.RenderApp(batch, width, height);
            batch.DrawCommandList(canvas, 0, 0, width, height);
            MauiCursor.SetMouseCursor(guiBackend, this);
        }

        /* using var paint = new SKPaint {
            Color = SKColors.Blue,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawCircle(200, 200, 100, paint); */
    }
}