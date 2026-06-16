using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;
using Friflo.Vectorization.WebGPU.Runtime;
using SDL3;

// ReSharper disable InconsistentNaming
namespace TestConsole;

public struct SdlWindow
{
    private nint            window;
    public  TextureFormat   swapChainFormat;
    
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
    
    /// <summary> Set the size of the surface to its window can configure the swapChainFormat. </summary>
    public void ConfigureSurface(WgpuSurface surface, GpuDevice device)
    {
        SDL.GetWindowSizeInPixels(window, out var pixelWidth, out var pixelHeight);
        var surfaceConfig = new SurfaceConfiguration {
            format      = swapChainFormat,
            usage       = WebGPU_native.TextureUsage_RenderAttachment,
            alphaMode   = CompositeAlphaMode.Opaque,
            width       = (uint)pixelWidth,
            height      = (uint)pixelHeight,
            presentMode = PresentMode.Fifo  // Fifo = VSync
        };
        surface.Configure(device, surfaceConfig);
    }
    
    /// <summary> Poll and handle basic SDL events like Quit and window resize. </summary>
    /// <remarks>
    /// <c>surface</c> and <c>device</c> are required to resize surface.
    /// </remarks>
    public bool PollEvents(WgpuSurface surface, GpuDevice device)
    {
        while (SDL.PollEvent(out var e))
        {
            switch (e.Type)
            {
                case (uint)SDL.EventType.Quit:
                    return false;
                case (uint)SDL.EventType.WindowRestored:
                case (uint)SDL.EventType.WindowExposed:
                case (uint)SDL.EventType.WindowPixelSizeChanged:
                    ConfigureSurface(surface, device);
                    break;
            }
        }
        return true;
    }
}