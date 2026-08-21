using System.Numerics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Friflo.GPU;
using Friflo.WGPU;
using Friflo.WGPU.ImDraw;
using SDL3;

// ReSharper disable InconsistentNaming
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

public class Wgpu
{
    public  readonly    WgpuInstance        Instance;
    public  readonly    GpuAdapter          Adapter;
    public  readonly    GpuDevice           Device;
    public  readonly    PipelineContext     Context;
    public  readonly    WgpuSurface         Surface;
    public  readonly    TextureFormat       SwapChainFormat;
    public  readonly    CompositeAlphaMode  AlphaMode;
    public  readonly    RenderConfig        Config;
    public              GpuExtent3D         TargetSize;
    
    public Wgpu(nint osHandle, nint osInstance)
    {
        Instance    = WgpuInstance.CreateInstance();
        Surface     = WgpuSurface.CreateFromNativeWindow(Instance, osHandle, osInstance);
        Adapter     = Instance.RequestAdapter(default); // specific backend: new GpuRequestAdapterOptions { backendType = BackendType.D3D12 }
        Device      = Adapter.CreateDevice("Wgpu.Device");
        Context     = Device.BeginContext();
        
        var fragmentState   = Surface.GetPreferredFragmentState(Adapter, true, out AlphaMode);
        SwapChainFormat     = fragmentState.targets[0].format;
        var desc            = new GpuRenderPipelineDescriptor { FragmentState = fragmentState };
        Config              = desc.CreateConfig("Wgpu.Config");
    }
    
    public void Shutdown()
    {
        Surface.Unconfigure();
        Context.Dispose();
        Device.Dispose();
        Adapter.Dispose();
        Surface.Dispose();
        var handleDiff = Instance.GenerateHandles();
        if (!handleDiff.IsActiveZero()) {
            Console.WriteLine(handleDiff.GetHandleLog("[GPU RESOURCE LEAK DETECTED]", true));
        }
        Instance.Dispose();
    }
}


public class SdlWindow(string title, int width, int height, Func<Wgpu, IRenderer> createRenderer)
{
    private             nint                   window;
    private             Wgpu?                  wgpu;
    private             IRenderer?             renderer;
    private             ExceptionDispatchInfo? callbackException;
    
    // --- fields for SDL3 input handling
    private readonly    Sld3Input               sdlInput = new();
    private             GuiModule?              guiModule;
    private             Vector2                 dpiScale;
    
    public static int Run(string title, int width, int height, Func<Wgpu, IRenderer> createRenderer)
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
            osInstance  = 0;
        } else {
            throw new NotImplementedException($"no code to setup SDL3 for OS: {RuntimeInformation.OSDescription}");
        }
        SDL.ShowWindow(window);
        
        // --- setup wgpu resources --- 
        wgpu = new Wgpu(osHandle, osInstance);
        var backend = wgpu.Adapter.GetAdapterInfo().BackendType;
        SDL.SetWindowTitle(window, $"{title} - {backend}");
        renderer = createRenderer(wgpu);
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
        if (wgpu!.TargetSize.width == pixelWidth && wgpu.TargetSize.height == pixelHeight) {
            return;
        }
        SDL.GetWindowSize(window, out int windowWidth, out int windowHeight);
        wgpu!.TargetSize = new GpuExtent3D(pixelWidth, pixelHeight, 1);
        if (pixelWidth == 0 || pixelHeight == 0) {
            return;
        }
        dpiScale.X = pixelWidth  / (float)windowWidth;
        dpiScale.Y = pixelHeight / (float)windowHeight;
        ConfigureSurface(pixelWidth, pixelHeight);
        renderer?.OnWindowChanged(pixelWidth, pixelHeight);
    }
    
    private void ConfigureSurface(int pixelWidth, int pixelHeight)
    {
        var surfaceConfig = new WgpuSurfaceConfiguration {
            device      = wgpu!.Device,
            format      = wgpu.SwapChainFormat,
            usage       = TextureUsage.RenderAttachment,
            alphaMode   = wgpu.AlphaMode,  // or CompositeAlphaMode.Opaque
            width       = pixelWidth,
            height      = pixelHeight,
            presentMode = PresentMode.Immediate // Fifo = VSync, Immediate = max
        };
        wgpu.Surface.Configure(surfaceConfig);
    }
    
    private SDL.AppResult IterateSdl3()
    {
        using var target = wgpu!.Context.BeginRenderTarget(wgpu.Surface, wgpu.TargetSize, "RenderTarget-Encoder"u8);
        if (target.IsNull) {     // window minimized?
            return SDL.AppResult.Continue;
        }
        guiModule = wgpu.Device.GetGuiModule();  
        guiModule?.NewFrame();
        
        renderer?.OnFrame(target);
        
        wgpu.Context.Queue.Submit();
        wgpu.Surface.Present();
        
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
        if (guiModule != null) {
            sdlInput.HandleGuiInput(guiModule, ev, dpiScale);
        }
        return SDL.AppResult.Continue;
    }
    
    private void Shutdown()
    {
        sdlInput.Dispose();
        Sdl3Cursor.Shutdown();
        renderer?.OnShutdown();
        renderer = null;
        wgpu?.Shutdown();
        wgpu = null;
        SDL.DestroyWindow(window);
        SDL.Quit();
    }
}


// ------------------------ SDL3 input handling: keyboard, mouse & gamepad ------------------------
internal class Sld3Input : IDisposable
{
    private nint gamepad;

    /// <summary>
    /// Use <c> dpiScale = new Vector2(1, 1 </c> is not available.
    /// </summary>
    internal void HandleGuiInput(GuiModule guiModule, in SDL.Event ev, Vector2 dpiScale)
    {
        var type = (SDL.EventType)ev.Type;
        switch (type)
        {
            case SDL.EventType.MouseMotion:     guiModule.AddEvent(new ImEvent(ImEventType.MouseMotion,     GetMousePos(dpiScale, ev)));  break;
            case SDL.EventType.MouseButtonUp:   guiModule.AddEvent(new ImEvent(ImEventType.MouseButtonUp,   GetMousePos(dpiScale, ev)));  break;
            case SDL.EventType.MouseButtonDown: guiModule.AddEvent(new ImEvent(ImEventType.MouseButtonDown, GetMousePos(dpiScale, ev)));  break;
            
            case SDL.EventType.KeyDown:
                var key = new KeyEvent { code = (KeyCode)ev.Key.Key, mod = (KeyMod)ev.Key.Mod, isDown = true };
                guiModule.AddEvent(new ImEvent { type = ImEventType.KeyDown, key = key });
                break;
            case SDL.EventType.KeyUp:
                key = new KeyEvent { code = (KeyCode)ev.Key.Key, mod = (KeyMod)ev.Key.Mod, isDown = false };
                guiModule.AddEvent(new ImEvent { type = ImEventType.KeyUp, key = key });
                break;
            
            case SDL.EventType.GamepadAdded:        gamepad = SDL.OpenGamepad(ev.JDevice.Which);    break;
            case SDL.EventType.GamepadRemoved:      CloseGamepad();                                 break;
            case SDL.EventType.GamepadButtonUp:     guiModule.AddEvent(new ImEvent(ImEventType.GamepadButtonUp,   (ImGamepadButton)ev.GButton.Button, false)); break;
            case SDL.EventType.GamepadButtonDown:   guiModule.AddEvent(new ImEvent(ImEventType.GamepadButtonDown, (ImGamepadButton)ev.GButton.Button, true));  break;
        }
    }
    
    private static Vector2 GetMousePos(Vector2 dpiScale, in SDL.Event ev) => new (dpiScale.X * ev.Button.X, dpiScale.Y * ev.Button.Y);

    private void CloseGamepad()
    {
        if (gamepad == 0) return;
        SDL.CloseGamepad(gamepad);
        gamepad = 0;
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
    private static readonly Dictionary<MouseCursor, IntPtr> cursorCache         = new();
    private static          MouseCursor                     currentCursorType   = MouseCursor.Arrow;

    internal static void Init() {
        cursorCache[MouseCursor.Arrow]      = SDL.CreateSystemCursor(SDL.SystemCursor.Default);
        cursorCache[MouseCursor.ResizeNS]   = SDL.CreateSystemCursor(SDL.SystemCursor.NSResize);
        cursorCache[MouseCursor.ResizeEW]   = SDL.CreateSystemCursor(SDL.SystemCursor.EWResize);
        cursorCache[MouseCursor.ResizeNWSE] = SDL.CreateSystemCursor(SDL.SystemCursor.NWSEResize);
        cursorCache[MouseCursor.ResizeNESW] = SDL.CreateSystemCursor(SDL.SystemCursor.NESWResize);
    }
    
    internal static void Shutdown()
    {
        foreach (var handle in cursorCache.Values) {
            if (handle != IntPtr.Zero) SDL.DestroyCursor(handle);
        }
        cursorCache.Clear();
    }

    public static void SetCursor(MouseCursor cursor)
    {
        if (cursor == currentCursorType) return;
        if (cursorCache.TryGetValue(cursor, out IntPtr handle)) {
            SDL.SetCursor(handle);
            currentCursorType = cursor;
        }
    }
}