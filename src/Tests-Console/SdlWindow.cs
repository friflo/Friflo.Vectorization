using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;
using Friflo.Vectorization.WebGPU.Runtime;
using SDL3;

// ReSharper disable MemberCanBePrivate.Global
namespace TestConsole;

public interface IRenderer
{
    public void DrawFrame();
    public void Shutdown();
}

public class Wgpu
{
    public  readonly    WgpuInstance            Instance;
    public  readonly    WgpuAdapter             Adapter;
    public  readonly    GpuDevice               Device;
    public  readonly    WgpuSurface             Surface;
    public  readonly    TextureFormat           SwapChainFormat;
    public  readonly    CompositeAlphaMode      AlphaMode;
    public  readonly    RenderPipelineConfig    Config;
    
    public Wgpu(nint osHandle, nint osInstance)
    {
        Instance    = WgpuInstance.CreateInstance(new InstanceExtras());
        Surface     = WgpuSurface.CreateFromNativeWindow(Instance, osHandle, osInstance);
        Adapter     = Instance.RequestAdapter(default, null);
        Device      = Adapter.CreateDevice("test");
        
        var fragmentState   = Surface.GetPreferredFragmentState(Adapter, true, out AlphaMode);
        SwapChainFormat     = fragmentState.targets[0].format;
        var desc            = new WgpuRenderPipelineDescriptor { FragmentState = fragmentState };
        Config              = desc.CreateConfig("render config");
    }
    
    public void Shutdown()
    {
        Device.Dispose();
        Adapter.Dispose();
        Instance.Dispose();
    }
}


public class SdlWindow
{
    private nint                    window;
    private Wgpu?                   wgpu;
    private IRenderer?              renderer;
    private ExceptionDispatchInfo?  callbackException;
    
    public void Main()
    {
        SDL.SetMainReady();
        SDL.EnterAppMainCallbacks(0, [], AppInit, AppIterate, AppEvent, AppQuit);
        callbackException?.Throw();
    }
    
    private SDL.AppResult AppInit(IntPtr appState, int argc, string[] argv)
    {
        try {
            renderer = new RenderTest(this);
            ConfigureSurface();
            return SDL.AppResult.Continue;
        }
        catch (Exception exception) {
            return Capture(exception);
        }
    }
    
    private void AppQuit(IntPtr appState, SDL.AppResult result)
    {
        try {
            renderer?.Shutdown();
            renderer = null;
            wgpu?.Shutdown();
            wgpu = null;
            SDL.DestroyWindow(window);
            SDL.Quit();
        }
        catch (Exception exception) {
            Capture(exception);
        }
    }
    
    private SDL.AppResult AppIterate(IntPtr appState)
    {
        try {
            renderer?.DrawFrame();
            return SDL.AppResult.Continue;
        }
        catch (Exception exception) {
            return Capture(exception);
        }
    }
    
    private SDL.AppResult AppEvent(IntPtr appState, ref SDL.Event ev)
    {
        try {
            var type = (SDL.EventType)ev.Type;
            switch (type)
            {
                case SDL.EventType.Quit:
                    return SDL.AppResult.Success;
                case SDL.EventType.WindowRestored:
                case SDL.EventType.WindowExposed:
                case SDL.EventType.WindowPixelSizeChanged:
                    ConfigureSurface();
                    break;
            }
            return SDL.AppResult.Continue;
        }
        catch (Exception exception) {
            return Capture(exception);
        }
    }
    
    private SDL.AppResult Capture(Exception exception)
    {
        callbackException ??= ExceptionDispatchInfo.Capture(exception);
        return SDL.AppResult.Failure;
    }
    
    /// <summary> Init SDL3 and create window </summary>
    public Wgpu InitSdl3(string title, int width, int height)
    {
        if (!SDL.Init(SDL.InitFlags.Video)) throw new Exception($"SDL3 initialization failed: {SDL.GetError()}");
        
        var windowFlags = SDL.WindowFlags.Hidden | SDL.WindowFlags.Resizable;
        if (OperatingSystem.IsMacOS()) {
            windowFlags |= SDL.WindowFlags.Metal | SDL.WindowFlags.HighPixelDensity;
        }
        window = SDL.CreateWindow(title, width, height, windowFlags);
        if (window == IntPtr.Zero)          throw new Exception($"Failed to create window: {SDL.GetError()}");

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
        
        return wgpu = new Wgpu(osHandle, osInstance);
    }
    
    private void ConfigureSurface()
    {
        SDL.GetWindowSizeInPixels(window, out var pixelWidth, out var pixelHeight);
        if (pixelWidth == 0 || pixelHeight == 0) return;
        
        var surfaceConfig = new SurfaceConfiguration {
            format      = wgpu!.SwapChainFormat,
            usage       = WebGPU_native.TextureUsage_RenderAttachment,
            alphaMode   = wgpu!.AlphaMode,  // or CompositeAlphaMode.Opaque
            width       = (uint)pixelWidth,
            height      = (uint)pixelHeight,
            presentMode = PresentMode.Immediate // Fifo = VSync
        }; 
        wgpu.Surface.Configure(wgpu.Device, surfaceConfig);
    }
}