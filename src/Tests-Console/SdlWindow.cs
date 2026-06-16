using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;
using Friflo.Vectorization.WebGPU.Runtime;
using SDL3;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable MemberCanBeProtected.Global
namespace TestConsole;

public abstract class Renderer
{
    public  readonly    WgpuInstance            Instance;
    public  readonly    WgpuAdapter             Adapter;
    public  readonly    GpuDevice               Device;
    public  readonly    WgpuSurface             Surface;
    public  readonly    TextureFormat           SwapChainFormat;
    public  readonly    CompositeAlphaMode      AlphaMode;
    public  readonly    RenderPipelineConfig    Config;
    
    public abstract void DrawFrame();

    public virtual void Shutdown()
    {
        Device.Dispose();
        Adapter.Dispose();
        Instance.Dispose();
    }
    
    protected Renderer(SdlWindow window, string title, int width, int height)
    {
        window.InitSdl3(title, width, height, out var osHandle, out var osInstance);
        
        Instance    = WgpuInstance.CreateInstance(new InstanceExtras());
        Surface     = WgpuSurface.CreateFromNativeWindow(Instance, osHandle, osInstance);
        Adapter     = Instance.RequestAdapter(default, null);
        Device      = Adapter.CreateDevice("test");
        
        var fragmentState   = Surface.GetPreferredFragmentState(Adapter, true, out AlphaMode);
        SwapChainFormat     = fragmentState.targets[0].format;
        var desc            = new WgpuRenderPipelineDescriptor { FragmentState = fragmentState };
        Config              = desc.CreateConfig("render config");
    }
}


public class SdlWindow
{
    private nint        window;
    private Renderer?   renderer;
    
    public void Main()
    {
        SDL.SetMainReady();
        SDL.EnterAppMainCallbacks(0, [], AppInit, AppIterate, AppEvent, AppQuit);
    }
    
    private SDL.AppResult AppInit(IntPtr appState, int argc, string[] argv)
    {
        renderer = new RenderTest(this);
        ConfigureSurface();
        return SDL.AppResult.Continue;
    }
    
    private void AppQuit(IntPtr appState, SDL.AppResult result) {
        renderer?.Shutdown();
        renderer = null;
    }
    
    private SDL.AppResult AppIterate(IntPtr appState)
    {
        renderer?.DrawFrame();
        return SDL.AppResult.Continue;
    }
    
    private SDL.AppResult AppEvent(IntPtr appState, ref SDL.Event ev)
    {
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
    
    /// <summary> Init SDL3 and create window </summary>
    public void InitSdl3(string title, int width, int height, out nint osHandle, out nint osInstance)
    {
        if (!SDL.Init(SDL.InitFlags.Video)) throw new Exception($"SDL3 initialization failed: {SDL.GetError()}");
        
        var windowFlags = SDL.WindowFlags.Hidden | SDL.WindowFlags.Resizable;
        if (OperatingSystem.IsMacOS()) {
            windowFlags |= SDL.WindowFlags.Metal | SDL.WindowFlags.HighPixelDensity;
        }
        window = SDL.CreateWindow(title, width, height, windowFlags);
        if (window == IntPtr.Zero)          throw new Exception($"Failed to create window: {SDL.GetError()}");

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
    }
    
    private void ConfigureSurface()
    {
        SDL.GetWindowSizeInPixels(window, out var pixelWidth, out var pixelHeight);
        if (pixelWidth == 0 || pixelHeight == 0) return;
        
        var surfaceConfig = new SurfaceConfiguration {
            format      = renderer!.SwapChainFormat,
            usage       = WebGPU_native.TextureUsage_RenderAttachment,
            alphaMode   = renderer!.AlphaMode,  // or CompositeAlphaMode.Opaque
            width       = (uint)pixelWidth,
            height      = (uint)pixelHeight,
            presentMode = PresentMode.Immediate // Fifo = VSync
        }; 
        renderer.Surface.Configure(renderer.Device, surfaceConfig);
    }
}