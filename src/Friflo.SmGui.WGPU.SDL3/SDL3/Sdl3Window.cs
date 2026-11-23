using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Friflo.SmGui;
using Friflo.SmGui.Runtime;
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
    public  string          title;
    public  int             width;
    public  int             height;
    public  CreateRenderer  createRenderer;
    public  CreateGuiApp?   createGuiApp;
}

public class Sdl3Window
{
    public      nint                    Window      => window;
    public      WgpuHost?               WgpuHost    => wgpuHost;
    public      IWgpuRenderer?          Renderer    => renderer;
    public      SmGuiBackend?           GuiBackend  => guiBackend;
    public      InitOptions             initOptions;
   
#region private fields
    private     nint                    window;
    private     WgpuHost?               wgpuHost;
    private     IWgpuRenderer?          renderer;
    private     ExceptionDispatchInfo?  callbackException;
    // --- SmGui fields
    private     SmGuiBackend?           guiBackend;
    private     Sdl3GuiInput?           sdlGuiInput;
    private     Sdl3Cursor?             sdlCursor;
    private     ProcessReadyEvents?     processSessionEvents;
#endregion
    
    public int EnterAppMain(InitOptions opt)
    {
        initOptions = opt;
        SDL.SetMainReady();
        var result = SDL.EnterAppMainCallbacks(0, null, AppInit, AppIterate, AppEvent, AppQuit);
        callbackException?.Throw();
        return result;
    }
    
#region SDL3 lifecycle callbacks
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
#endregion
    
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
        
        // --- setup wgpu resources 
        wgpuHost    = new WgpuHost(osHandle, osInstance, driver);
        renderer    = initOptions.createRenderer(wgpuHost);
        if (initOptions.createGuiApp != null) {
            var app = renderer.CreateGuiApp(initOptions.createGuiApp);
            var loop = app?.SessionLoop;
            if (loop != null) {
                loop.RuntimeEventHandler = Sdl3RuntimeEventHandler.Instance;
                processSessionEvents = loop.IterateSessions;
                Sdl3RuntimeEventHandler.Instance.ProcessReadyEvents += processSessionEvents;
            }
        }
        SetWindowSize();

        // --- setup SmGui (optional)
        guiBackend  = renderer.GuiBackend;
        if (guiBackend != null) {
            guiBackend.SetClientActions(new Sdl3ClientAction(window));
            sdlGuiInput = new Sdl3GuiInput();
            sdlCursor   = new Sdl3Cursor();
        }
    }
    
    
    protected virtual void OnSystemThemeChanged() => SetDefaultWindowIcon();
    
    public void SetDefaultWindowIcon()
    {
        var theme = SDL.GetSystemTheme();
        var image = theme == SDL.SystemTheme.Dark ? "Friflo.WGPU.SDL3.Assets.wgpu-transparent-dark-128x128.bmp.gz" : 
                                                    "Friflo.WGPU.SDL3.Assets.wgpu-transparent-light-128x128.bmp.gz";
        
        using var stream = typeof(Sdl3Window).Assembly.GetManifestResourceStream(image);
        if (stream != null) {
            using var gzipStream = new GZipStream(stream, CompressionMode.Decompress);
            SetWindowIcon(gzipStream);
        }
    }
    
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
        
        if (guiBackend != null) sdlCursor!.SetCursor(guiBackend.input.CurrentCursor);
        
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
            default:
                if (ev.Type == Sdl3RuntimeEventHandler.NotifyEventReadyType) {
                    Sdl3RuntimeEventHandler.Instance.ProcessEvents(); // calls SmSessionLoop.IterateSessions()
                }
                break;
        }
        if (guiBackend != null && wgpuHost != null) {
            sdlGuiInput!.HandleGuiInput(guiBackend, ev, wgpuHost.DpiScale);
        }
        return SDL.AppResult.Continue;
    }
    
    protected virtual void OnQuit()
    {
        Sdl3RuntimeEventHandler.Instance.ProcessReadyEvents -= processSessionEvents;
        
        guiBackend?.Dispose();
        guiBackend = null;
        
        sdlGuiInput?.Dispose();
        sdlGuiInput = null;
        
        sdlCursor?.Shutdown();
        sdlCursor = null;
        
        renderer?.OnShutdown();
        renderer = null;
        
        wgpuHost?.Shutdown();
        wgpuHost = null;
        
        SDL.DestroyWindow(window);
        window = IntPtr.Zero;
        
        SDL.Quit();
    }
}


internal sealed class Sdl3RuntimeEventHandler : SmRuntimeEventHandler
{
    internal static readonly Sdl3RuntimeEventHandler    Instance = new();
    internal static readonly uint                       NotifyEventReadyType = SDL.RegisterEvents(1);
    
    private Sdl3RuntimeEventHandler() { }
    
    protected override void OnNotifyEventReady()
    {
        var ev = new SDL.Event {
            Type = NotifyEventReadyType,
            User = new SDL.UserEvent { Type = NotifyEventReadyType, Timestamp = SDL.GetTicksNS(), Code = 0 }
        };
        SDL.PushEvent(ref ev);
    }
}