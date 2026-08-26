// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using Friflo.GPU;


// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;

internal sealed class DrawModule : IGpuDeviceModule
{
    internal readonly   GpuSampler      samplerLinear;
    internal readonly   GpuSampler      samplerNearest;
    internal readonly   Font            defaultFont;
    internal readonly   GuiModule       guiModule;
    
    
    internal DrawModule(GpuDevice device)
    {
        samplerLinear  = device.CreateSampler(new GpuSamplerDescriptor { label = "Linear Sampler",  magFilter = FilterMode.Linear,  minFilter = FilterMode.Linear  });
        samplerNearest = device.CreateSampler(new GpuSamplerDescriptor { label = "Nearest Sampler", magFilter = FilterMode.Nearest, minFilter = FilterMode.Nearest });
        
        defaultFont = device.CreateDefaultFont();

        guiModule = new GuiModule();
    }
    
    public void Dispose()
    {
        guiModule.Dispose();
        samplerLinear.Dispose();
        samplerNearest.Dispose();
        defaultFont.DisposeInternal();
    }
}
