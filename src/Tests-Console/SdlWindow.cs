using System.Numerics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Friflo.WGPU;
using Friflo.WGPU.ImDraw;
using SDL3;


// ReSharper disable MemberCanBePrivate.Global
namespace TestConsole;

/// <summary>
/// Enables an event driven approach - DrawFrame() + Shutdown() - instead of running an event loop.<br/>
/// This approach ensures the same renderer can be used on mobile devices or browsers without code changes.<br/>
/// Those platforms only support event driven applications. An event loop would lead to application freeze.
/// </summary>
public interface IRenderer
{
    public void OnWindowChanged(int width, int height) { }
    public void OnFrame        (in RenderTarget target);
    public void OnShutdown();
}

public class SdlWindow(string title, int width, int height, Func<WgpuHost, IRenderer> createRenderer)
{
    private             nint                   window;
    private             WgpuHost?              wgpuHost;
    private             IRenderer?             renderer;
    private             ExceptionDispatchInfo? callbackException;
    
    // --- fields for SDL3 input handling
    private readonly    Sdl3Input               sdlInput = new();
    private             GuiModule?              guiModule;
    
    public static int Run(string title, int width, int height, Func<WgpuHost, IRenderer> createRenderer)
    {
        var sdl = new SdlWindow(title + " - friflo GPU", width, height, createRenderer);
        SDL.SetMainReady();
        var result = SDL.EnterAppMainCallbacks(0, null, sdl.AppInit, sdl.AppIterate, sdl.AppEvent, sdl.AppQuit);
        sdl.callbackException?.Throw();
        return result;
    }

    private SDL.AppResult AppInit(ref IntPtr appState, int argc, string[]? argv)
    {
        try { return InitSdl3(); }
        catch (Exception exception)   { return Capture(exception); }
    }
    
    private void AppQuit(IntPtr appState, SDL.AppResult result)
    {
        try { Shutdown(); }
        catch (Exception exception)   { Capture(exception); }
    }
    
    private SDL.AppResult AppIterate(IntPtr appState)
    {
        try { return IterateSdl3(); }
        catch (Exception exception)   { return Capture(exception); }
    }
    
    private SDL.AppResult AppEvent(IntPtr appState, ref SDL.Event ev)
    {
        try { return AppEvent(ev); }
        catch (Exception exception)   { return Capture(exception); }
    }
    
    private SDL.AppResult Capture(Exception exception)
    {
        callbackException ??= ExceptionDispatchInfo.Capture(exception);
        return SDL.AppResult.Failure;
    }
    
    /// <summary> Init SDL3 and create window </summary>
    public SDL.AppResult InitSdl3()
    {
        // --- setup SDL window ---
        if (!SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Gamepad)) throw new Exception($"SDL3 initialization failed: {SDL.GetError()}");
        
        var windowFlags = SDL.WindowFlags.Hidden | SDL.WindowFlags.Resizable;
        if (OperatingSystem.IsMacOS()) {
            windowFlags |= SDL.WindowFlags.Metal | SDL.WindowFlags.HighPixelDensity;
        }
        window = SDL.CreateWindow(title, width, height, windowFlags);
        if (window == IntPtr.Zero)          throw new Exception($"Failed to create window: {SDL.GetError()}");

        SetWindowIconFromResource();
        nint osHandle;
        nint osInstance;
        var props   = SDL.GetWindowProperties(window);
        if (OperatingSystem.IsWindows()) {
            osHandle    = SDL.GetPointerProperty(props, SDL.Props.WindowWin32HWNDPointer,       IntPtr.Zero);
            osInstance  = SDL.GetPointerProperty(props, SDL.Props.WindowWin32InstancePointer,   IntPtr.Zero);
        } else if (OperatingSystem.IsMacOS()) {
            osHandle    = SDL.GetPointerProperty(props, SDL.Props.WindowCocoaWindowPointer,     IntPtr.Zero);
            osInstance  = IntPtr.Zero;
        } else {
            throw new NotImplementedException($"no code to setup SDL3 for OS: {RuntimeInformation.OSDescription}");
        }
        SDL.ShowWindow(window);
        
        // --- setup wgpu resources --- 
        wgpuHost    = new WgpuHost(osHandle, osInstance);
        var backend = wgpuHost.Adapter.GetAdapterInfo().BackendType;
        SDL.SetWindowTitle(window, $"{title} - {backend}");
        renderer = createRenderer(wgpuHost);
        SetWindowSize();
        Sdl3Cursor.Init();
        return SDL.AppResult.Continue;
    }
    
    public void SetWindowIconFromResource()
    {
        var theme = SDL.GetSystemTheme();
        var image = theme == SDL.SystemTheme.Dark ? "Tests-Console.Assets.wgpu-transparent-dark-128x128.bmp" : 
                                                    "Tests-Console.Assets.wgpu-transparent-light-128x128.bmp";
        using var stream = typeof(SdlWindow).Assembly.GetManifestResourceStream(image);
        if (stream == null) {
            return;
        }
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
    
    
    private SDL.AppResult IterateSdl3()
    {
        if (wgpuHost == null) return SDL.AppResult.Continue;
        
        using var target = wgpuHost.Context.BeginRenderTarget(wgpuHost.Surface, wgpuHost.TargetSize, "RenderTarget-Encoder"u8);
        if (target.IsNull) {     // window minimized?
            return SDL.AppResult.Continue;
        }
        guiModule = wgpuHost.Device.GetGuiModule();  
        guiModule?.NewFrame();
        
        renderer?.OnFrame(target);
        
        if (guiModule != null) Sdl3Cursor.SetCursor(guiModule.input.CurrentCursor);
        
        wgpuHost.Context.Queue.Submit();
        wgpuHost.Surface.Present();
        
        return SDL.AppResult.Continue;
    }
    
    private SDL.AppResult AppEvent(in SDL.Event ev)
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
                SetWindowIconFromResource();
                break;
        }
        if (guiModule != null && wgpuHost != null) {
            sdlInput.HandleGuiInput(guiModule, ev, wgpuHost.DpiScale);
        }
        return SDL.AppResult.Continue;
    }
    
    private void Shutdown()
    {
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


// -------- SDL3 input handling: keyboard, mouse & gamepad - required when using:  Friflo.WGPU.ImDraw --------
internal class Sdl3Input : IDisposable
{
    private nint gamepad;

    /// <summary>
    /// Use <c> dpiScale = new Vector2(1, 1) </c> if not available.
    /// </summary>
    internal void HandleGuiInput(GuiModule guiModule, in SDL.Event ev, Vector2 dpiScale)
    {
        var type = (SDL.EventType)ev.Type;
        switch (type)
        {
            case SDL.EventType.MouseMotion:
                var motionPos = new Vector2(dpiScale.X * ev.Motion.X, dpiScale.Y * ev.Motion.Y);
                guiModule.AddEvent(new ImEvent(ImEventType.MouseMotion, motionPos));
                break;
            case SDL.EventType.MouseButtonUp:
                var buttonUpPos = new Vector2(dpiScale.X * ev.Button.X, dpiScale.Y * ev.Button.Y);
                guiModule.AddEvent(new ImEvent(ImEventType.MouseButtonUp, buttonUpPos));
                break;
            case SDL.EventType.MouseButtonDown:
                var buttonDownPos = new Vector2(dpiScale.X * ev.Button.X, dpiScale.Y * ev.Button.Y);
                guiModule.AddEvent(new ImEvent(ImEventType.MouseButtonDown, buttonDownPos));
                break;
            case SDL.EventType.KeyDown:
                var key = new KeyEvent { code = (KeyCode)ev.Key.Key, mod = (KeyMod)ev.Key.Mod, isDown = true };
                guiModule.AddEvent(new ImEvent(ImEventType.KeyDown, key));
                break;
            case SDL.EventType.KeyUp:
                key = new KeyEvent { code = (KeyCode)ev.Key.Key, mod = (KeyMod)ev.Key.Mod, isDown = false };
                guiModule.AddEvent(new ImEvent(ImEventType.KeyUp, key));
                break;
            
            case SDL.EventType.GamepadAdded:        gamepad = SDL.OpenGamepad(ev.JDevice.Which);    break;
            case SDL.EventType.GamepadRemoved:      CloseGamepad();                                 break;
            case SDL.EventType.GamepadButtonUp:
                guiModule.AddEvent(new ImEvent(ImEventType.GamepadButtonUp,   (ImGamepadButton)ev.GButton.Button, false));
                break;
            case SDL.EventType.GamepadButtonDown:
                guiModule.AddEvent(new ImEvent(ImEventType.GamepadButtonDown, (ImGamepadButton)ev.GButton.Button, true));
                break;
        }
    }

    private void CloseGamepad()
    {
        if (gamepad == IntPtr.Zero) return;
        SDL.CloseGamepad(gamepad);
        gamepad = IntPtr.Zero;
    }

    public void Dispose()
    {
        CloseGamepad();
    }
}

/// <summary>
/// Only required to visualize window resize indicator with <see cref="Friflo.WGPU.ImDraw.GuiInput.CurrentCursor"/> .
/// </summary>
internal static class Sdl3Cursor
{
    private static readonly Dictionary<MouseCursor, IntPtr> CursorCache         = new();
    private static          MouseCursor                     _currentCursorType  = MouseCursor.Arrow;

    internal static void Init() {
        CursorCache[MouseCursor.Arrow]      = SDL.CreateSystemCursor(SDL.SystemCursor.Default);
        CursorCache[MouseCursor.ResizeNS]   = SDL.CreateSystemCursor(SDL.SystemCursor.NSResize);
        CursorCache[MouseCursor.ResizeEW]   = SDL.CreateSystemCursor(SDL.SystemCursor.EWResize);
        CursorCache[MouseCursor.ResizeNWSE] = SDL.CreateSystemCursor(SDL.SystemCursor.NWSEResize);
        CursorCache[MouseCursor.ResizeNESW] = SDL.CreateSystemCursor(SDL.SystemCursor.NESWResize);
    }
    
    internal static void Shutdown()
    {
        foreach (var handle in CursorCache.Values) {
            if (handle != IntPtr.Zero) SDL.DestroyCursor(handle);
        }
        CursorCache.Clear();
    }

    public static void SetCursor(MouseCursor cursor)
    {
        if (cursor == _currentCursorType) return;
        if (CursorCache.TryGetValue(cursor, out IntPtr handle)) {
            SDL.SetCursor(handle);
            _currentCursorType = cursor;
        }
    }
}