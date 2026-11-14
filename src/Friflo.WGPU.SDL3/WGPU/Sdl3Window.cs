using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Friflo.SmGui;
using SDL3;

// ReSharper disable MemberCanBeProtected.Global
// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedMember.Local
// ReSharper disable VirtualMemberNeverOverridden.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable CheckNamespace
namespace Friflo.WGPU.SDL3;

public struct InitOptions {
    public string                    title;
    public int                       width;
    public int                       height;
    public Func<WgpuHost, IRenderer> createRenderer;
}

public class Sdl3Window
{
    public              nint                    Window      => window;
    public              WgpuHost?               WgpuHost    => wgpuHost;
    public              IRenderer?              Renderer    => renderer;
    public              SmGuiBackend?           GuiBackend  => guiBackend;
    
    public              InitOptions             initOptions;
    private             nint                    window;
    private             WgpuHost?               wgpuHost;
    private             IRenderer?              renderer;
    private             SmGuiBackend?           guiBackend;
    private             ExceptionDispatchInfo?  callbackException;
    private readonly    Sdl3Input               sdlInput = new();
    
    protected int EnterAppMain(InitOptions opt)
    {
        initOptions = opt;
        SDL.SetMainReady();
        var result = SDL.EnterAppMainCallbacks(0, null, AppInit, AppIterate, AppEvent, AppQuit);
        callbackException?.Throw();
        return result;
    }

    private SDL.AppResult AppInit(ref IntPtr appState, int argc, string[]? argv)
    {
        try { return OnInit(); }
        catch (Exception exception)   { return Capture(exception); }
    }
    
    private void AppQuit(IntPtr appState, SDL.AppResult result)
    {
        try { OnQuit(); }
        catch (Exception exception)   { Capture(exception); }
    }
    
    private SDL.AppResult AppIterate(IntPtr appState)
    {
        try { return OnIterate(); }
        catch (Exception exception)   { return Capture(exception); }
    }
    
    private SDL.AppResult AppEvent(IntPtr appState, ref SDL.Event ev)
    {
        try { return OnEvent(ev); }
        catch (Exception exception)   { return Capture(exception); }
    }
    
    private SDL.AppResult Capture(Exception exception)
    {
        callbackException ??= ExceptionDispatchInfo.Capture(exception);
        return SDL.AppResult.Failure;
    }
    
    protected virtual void CreateWindow()
    {
        var windowFlags = SDL.WindowFlags.Hidden | SDL.WindowFlags.Resizable;
        if (OperatingSystem.IsMacOS()) {
            windowFlags |= SDL.WindowFlags.Metal | SDL.WindowFlags.HighPixelDensity;
        }
        window = SDL.CreateWindow(initOptions.title, initOptions.width, initOptions.height, windowFlags);
        if (window == IntPtr.Zero) throw new Exception($"Failed to create window: {SDL.GetError()}");
    }
    
    /// <summary> Init SDL3 and create window </summary>
    protected virtual SDL.AppResult OnInit()
    {
        CreateWindow();

        OnSystemThemeChanged();

        SDL.ShowWindow(window);
        
        CreateWgpuHost();
        
        var backend = wgpuHost?.Adapter.GetAdapterInfo().BackendType;
        SDL.SetWindowTitle(window, $"{initOptions.title} - {backend}");
        
        return SDL.AppResult.Continue;
    }
    
    protected void CreateWgpuHost()
    {
        nint osHandle;
        nint osInstance;
        var props   = SDL.GetWindowProperties(window);
        string driver;
        if (OperatingSystem.IsWindows()) {
            driver      = "Windows";
            osHandle    = SDL.GetPointerProperty(props, SDL.Props.WindowWin32HWNDPointer,       IntPtr.Zero);
            osInstance  = SDL.GetPointerProperty(props, SDL.Props.WindowWin32InstancePointer,   IntPtr.Zero);
        } else if (OperatingSystem.IsMacOS()) {
            driver      = "macOS";
            osHandle    = SDL.GetPointerProperty(props, SDL.Props.WindowCocoaWindowPointer,     IntPtr.Zero);
            osInstance  = IntPtr.Zero;
        } else if (OperatingSystem.IsLinux()) {
            // Try Wayland first, fall back to X11
            var waylandDisplay = SDL.GetPointerProperty(props, SDL.Props.WindowWaylandDisplayPointer, IntPtr.Zero);
            var waylandSurface = SDL.GetPointerProperty(props, SDL.Props.WindowWaylandSurfacePointer, IntPtr.Zero);

            if (waylandDisplay != IntPtr.Zero && waylandSurface != IntPtr.Zero) {
                driver     = "Wayland";
                osInstance = waylandDisplay; // Display handle
                osHandle   = waylandSurface; // Surface handle
            } else {
                // X11 fallback
                driver     = "X11";
                osInstance =       SDL.GetPointerProperty(props, SDL.Props.WindowX11DisplayPointer, IntPtr.Zero);
                osHandle   = (nint)SDL.GetNumberProperty (props, SDL.Props.WindowX11WindowNumber, 0);
            }
        } else {
            throw new NotImplementedException($"no code to setup SDL3 for OS: {RuntimeInformation.OSDescription}");
        }
        
        // --- setup wgpu resources --- 
        wgpuHost    = new WgpuHost(osHandle, osInstance, driver);
        renderer    = initOptions.createRenderer(wgpuHost);
        guiBackend  = renderer.GuiBackend;
        guiBackend?.SetClientActions(new Sdl3ClientAction(window));
        SetWindowSize();
        Sdl3Cursor.Init();
    }
    
    protected virtual void OnSystemThemeChanged() { }
    
    public void SetWindowIcon(Stream stream)
    {
        var io = SDL.IOFromStream(stream);
        var surface = SDL.LoadBMPIO(io.Handle, false);
        if (surface != IntPtr.Zero) {
            SDL.SetWindowIcon(window, surface);
            SDL.DestroySurface(surface);
        }
    }
    
    private void SetWindowSize()
    {
        SDL.GetWindowSizeInPixels(window, out var pixelWidth, out var pixelHeight);
        SDL.GetWindowSize(window, out int windowWidth, out int windowHeight);
        
        wgpuHost?.ResizeTarget(pixelWidth, pixelHeight, windowWidth, windowHeight);
        renderer?.OnWindowChanged(pixelWidth, pixelHeight);
    }
    
    
    protected virtual SDL.AppResult OnIterate()
    {
        if (wgpuHost == null) return SDL.AppResult.Continue;
        
        using var target = wgpuHost.Context.BeginRenderTarget(wgpuHost.Surface, wgpuHost.TargetSize, "RenderTarget-Encoder"u8);
        if (target.IsNull) {     // window minimized?
            return SDL.AppResult.Continue;
        }
        guiBackend?.NewFrame();
        
        renderer?.OnFrame(target);
        
        if (guiBackend != null) Sdl3Cursor.SetCursor(guiBackend.input.CurrentCursor);
        
        wgpuHost.Context.Queue.Submit();
        wgpuHost.Surface.Present();
        
        return SDL.AppResult.Continue;
    }
    
    protected virtual SDL.AppResult OnEvent(in SDL.Event ev)
    {
        var type = (SDL.EventType)ev.Type;
        switch (type)
        {
            case SDL.EventType.Quit:
                return SDL.AppResult.Success;
            case SDL.EventType.WindowRestored:
            case SDL.EventType.WindowExposed:
            case SDL.EventType.WindowPixelSizeChanged:
                SetWindowSize();
                break;
            case SDL.EventType.SystemThemeChanged:
                OnSystemThemeChanged();
                break;
        }
        if (guiBackend != null && wgpuHost != null) {
            sdlInput.HandleGuiInput(guiBackend, ev, wgpuHost.DpiScale);
        }
        return SDL.AppResult.Continue;
    }
    
    protected virtual void OnQuit()
    {
        guiBackend?.Dispose();
        sdlInput.Dispose();
        Sdl3Cursor.Shutdown();
        renderer?.OnShutdown();
        renderer = null;
        wgpuHost?.Shutdown();
        wgpuHost = null;
        SDL.DestroyWindow(window);
        SDL.Quit();
    }
}
