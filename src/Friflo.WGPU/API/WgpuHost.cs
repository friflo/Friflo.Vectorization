// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Numerics;
using Friflo.GPU;
using Friflo.WGPU.Runtime;

// ReSharper disable UnassignedField.Global
// ReSharper disable InconsistentNaming
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable ConvertToConstant.Global
// ReSharper disable once CheckNamespace
namespace Friflo.WGPU;

public class WgpuHostOptions
{
    public InstanceExtras           instanceExtras;
    /// <summary> For different backend set <see cref="GpuRequestAdapterOptions.backendType"/>. E.g. <c>D3D12</c> </summary>
    public GpuRequestAdapterOptions adapterOptions;
    public DeviceDescriptor         deviceDescriptor;
    public bool                     useNonSrgb          = true;
    public int                      uniformBufferSize   = 64 * 1024;
}


public class WgpuHost
{
    // --- Immutable Core ---
    public  readonly    WgpuInstance        Instance;
    public  readonly    GpuAdapter          Adapter;
    public  readonly    GpuDevice           Device;
    public  readonly    PipelineContext     Context;
    public  readonly    WgpuSurface         Surface;
    public  readonly    TextureFormat       SwapChainFormat;
    public  readonly    RenderConfig        Config;
    
    // --- Dynamic Surface State ---
    public              CompositeAlphaMode  AlphaMode   { get; private set; }
    public              PresentMode         PresentMode { get; private set; } = PresentMode.Immediate; //  Fifo = VSync, Immediate = max
    public              GpuExtent3D         TargetSize  { get; private set; }
    public              Vector2             DpiScale    { get; private set; }

    
    public WgpuHost(nint osHandle, nint osInstance, WgpuHostOptions options = null)
    {
        options   ??= new WgpuHostOptions();
        Instance    = WgpuInstance.CreateInstance(options.instanceExtras);
        Surface     = WgpuSurface.CreateFromNativeWindow(Instance, osHandle, osInstance);
        var adapter = Instance.RequestAdapter(options.adapterOptions);
        Adapter     = adapter;
        Device      = adapter.CreateWgpuDevice("Wgpu.Device", options.deviceDescriptor, options.uniformBufferSize);
        Context     = Device.BeginContext();
        
        var fragmentState   = Surface.GetPreferredFragmentState(Adapter, options.useNonSrgb, out var alphaMode);
        AlphaMode           = alphaMode;
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

    /// <summary>Resizes the framebuffer target and updates DPI scale.</summary>
    /// <remarks>On standard-DPI displays, window dimensions match pixel dimensions.</remarks>
    public bool ResizeTarget(int pixelWidth, int pixelHeight, int windowWidth, int windowHeight)
    {
        if (pixelWidth <= 0 || pixelHeight <= 0 || windowWidth <= 0 || windowHeight <= 0) {
            TargetSize = new GpuExtent3D(0, 0, 1);
            return false;
        }
        if (TargetSize.width == pixelWidth && TargetSize.height == pixelHeight) {
            return false;
        }
        TargetSize  = new GpuExtent3D(pixelWidth, pixelHeight, 1);
        DpiScale    = new Vector2(pixelWidth  / (float)windowWidth, pixelHeight / (float)windowHeight);
        ConfigureSurface(pixelWidth, pixelHeight);
        return true;
    }
    
    public void SetPresentMode(PresentMode mode)
    {
        if (PresentMode == mode) {
            return;
        }
        PresentMode = mode;
        ConfigureSurface(TargetSize.width, TargetSize.height);
    }
    
    public void SetAlphaMode(CompositeAlphaMode mode)
    {
        if (AlphaMode == mode) {
            return;
        }
        AlphaMode = mode;
        ConfigureSurface(TargetSize.width, TargetSize.height);
    }
    
    private void ConfigureSurface(int pixelWidth, int pixelHeight)
    {
        if (pixelWidth <= 0 || pixelHeight <= 0) {
            return;
        }
        var surfaceConfig = new WgpuSurfaceConfiguration {
            device      = Device,
            format      = SwapChainFormat,
            usage       = TextureUsage.RenderAttachment,
            alphaMode   = AlphaMode,
            width       = pixelWidth,
            height      = pixelHeight,
            presentMode = PresentMode
        };
        Surface.Configure(surfaceConfig);
    }
}
