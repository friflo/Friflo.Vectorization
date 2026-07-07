using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Friflo.Vectorization.WebGPU;
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
    public void OnFrame        (int width, int height);
    public void OnShutdown();
}

public class Wgpu
{
    public  readonly    WgpuInstance        Instance;
    public  readonly    WgpuAdapter         Adapter;
    public  readonly    WgpuDevice          Device;
    public  readonly    WgpuSurface         Surface;
    public  readonly    TextureFormat       SwapChainFormat;
    public  readonly    CompositeAlphaMode  AlphaMode;
    public  readonly    RenderConfig        Config;
    public              int                 Width;
    public              int                 Height;
    
    public Wgpu(nint osHandle, nint osInstance)
    {
        Instance    = WgpuInstance.CreateInstance();
        Surface     = WgpuSurface.CreateFromNativeWindow(Instance, osHandle, osInstance);
        Adapter     = Instance.RequestAdapter(default); // specific backend: new GpuRequestAdapterOptions { backendType = BackendType.D3D12 }
        Device      = Adapter.CreateDevice("Wgpu.Device");
        
        var fragmentState   = Surface.GetPreferredFragmentState(Adapter, true, out AlphaMode);
        SwapChainFormat     = fragmentState.targets[0].format;
        var desc            = new GpuRenderPipelineDescriptor { FragmentState = fragmentState };
        Config              = desc.CreateConfig("Wgpu.Config");
    }
    
    public void Shutdown()
    {
        Surface.Unconfigure();
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
    private nint                    window;
    private Wgpu?                   wgpu;
    private IRenderer?              renderer;
    private ExceptionDispatchInfo?  callbackException;
    
    public static int Main(string title, int width, int height, Func<Wgpu, IRenderer> createRenderer)
    {
        var sdl = new SdlWindow(title + " - friflo GPU", width, height, createRenderer);
        SDL.SetMainReady();
        var result = SDL.EnterAppMainCallbacks(0, null, sdl.AppInit, sdl.AppIterate, sdl.AppEvent, sdl.AppQuit);
        sdl.callbackException?.Throw();
        return result;
    }

    private SDL.AppResult AppInit(IntPtr appState, int argc, string[]? argv)
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
        try { renderer?.OnFrame(wgpu!.Width, wgpu.Height);    return SDL.AppResult.Continue; }
        catch (Exception exception)   { return Capture(exception); }
    }
    
    private SDL.AppResult AppEvent(IntPtr appState, ref SDL.Event ev)
    {
        try { return AppEvent(ref ev); }
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
        if (!SDL.Init(SDL.InitFlags.Video)) throw new Exception($"SDL3 initialization failed: {SDL.GetError()}");
        
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
        if (wgpu!.Width == pixelWidth && wgpu.Height == pixelHeight) {
            return;
        }
        wgpu!.Width = pixelWidth;
        wgpu.Height = pixelHeight;
        if (pixelWidth == 0 || pixelHeight == 0) {
            return;
        }
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
            presentMode = PresentMode.Immediate // Fifo = VSync
        };
        wgpu.Surface.Configure(surfaceConfig);
    }
    
    private SDL.AppResult AppEvent(ref SDL.Event ev)
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
        return SDL.AppResult.Continue;
    }
    
    private void Shutdown()
    {
        renderer?.OnShutdown();
        renderer = null;
        wgpu?.Shutdown();
        wgpu = null;
        SDL.DestroyWindow(window);
        SDL.Quit();
    }
}