using System.Runtime.InteropServices;
using Friflo.Vectorization.WebGPU.Runtime;
using SDL3;

// ReSharper disable InconsistentNaming
namespace TestConsole;

public class SdlWindow
{
    private nint        	window;
    private RendererTest?   renderer;
    
    public void Main()
    {
        SDL.SetMainReady();
        SDL.EnterAppMainCallbacks(0, [], AppInit, AppIterate, AppEvent, AppQuit);
    }
    
    private SDL.AppResult AppInit(IntPtr appState, int argc, string[] argv)
    {
        renderer = new RendererTest(this);
        ConfigureSurface();
        return SDL.AppResult.Continue;
    }
    
    private void AppQuit(IntPtr appState, SDL.AppResult result) {
        renderer?.Dispose();
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
    public void InitSDL3(int width, int height, out nint osHandle, out nint osInstance)
    {
        if (!SDL.Init(SDL.InitFlags.Video)) throw new Exception($"SDL3 initialization failed: {SDL.GetError()}");
        
        var windowFlags = SDL.WindowFlags.Hidden | SDL.WindowFlags.Resizable;
        if (OperatingSystem.IsMacOS()) {
            windowFlags |= SDL.WindowFlags.Metal | SDL.WindowFlags.HighPixelDensity;
        }
        window = SDL.CreateWindow("friflo GPU", width, height, windowFlags);
        if (window == IntPtr.Zero)          throw new Exception($"Failed to create window: {SDL.GetError()}");

        var props   = SDL.GetWindowProperties(window);
        if (OperatingSystem.IsWindows()) {
            osHandle    = SDL.GetPointerProperty(props, SDL.Props.WindowWin32HWNDPointer,       IntPtr.Zero);
            osInstance  = SDL.GetPointerProperty(props, SDL.Props.WindowWin32InstancePointer,   IntPtr.Zero);
        } else if (OperatingSystem.IsMacOS()) {
            osHandle    = SDL.GetPointerProperty(props, SDL.Props.WindowCocoaWindowPointer,     IntPtr.Zero);
            osInstance  = 0;
        } else {
            throw new NotImplementedException($"no SDL3 setup code for OS: {RuntimeInformation.OSDescription}");
        }
        SDL.ShowWindow(window);
    }
    
    private void ConfigureSurface()
    {
        SDL.GetWindowSizeInPixels(window, out var pixelWidth, out var pixelHeight);
        if (pixelWidth == 0 || pixelHeight == 0) return;
        
        var surfaceConfig = new SurfaceConfiguration {
            format      = renderer!.swapChainFormat,
            usage       = WebGPU_native.TextureUsage_RenderAttachment,
            alphaMode   = CompositeAlphaMode.Opaque,
            width       = (uint)pixelWidth,
            height      = (uint)pixelHeight,
            presentMode = PresentMode.Immediate  // Fifo = VSync
        };
        renderer.surface.Configure(renderer.device, surfaceConfig);
    }
}