// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;


// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;

internal sealed class DrawModule : IGpuDeviceModule
{
    internal readonly   GpuSampler      samplerLinear;
    internal readonly   GpuSampler      samplerNearest;
//  private  readonly   GpuTexture      defaultWhiteTexture;
//  internal readonly   ImTextureView   defaultWhiteTextureView;
    internal readonly   Font            defaultFont;
    internal readonly   Gui             gui;
    internal readonly   GuiInput        input;
    
    
    internal DrawModule(GpuDevice device)
    {
        samplerLinear  = device.CreateSampler(new GpuSamplerDescriptor { label = "Linear Sampler",  magFilter = FilterMode.Linear,  minFilter = FilterMode.Linear  });
        samplerNearest = device.CreateSampler(new GpuSamplerDescriptor { label = "Nearest Sampler", magFilter = FilterMode.Nearest, minFilter = FilterMode.Nearest });
        
        defaultFont = device.CreateDefaultFont();
        
        input   = new GuiInput();
        gui     = new Gui(input);
        
        // --- Texture
        /* default white texture not used anymore - white pixel is in defaultFont
         
        defaultWhiteTexture = device.CreateTexture(new GpuTextureDescriptor {
            label   = "white1x1",
            size    = [1, 1],
            format  = TextureFormat.RGBA8Unorm,
            usage   = TextureUsage.TextureBinding | TextureUsage.CopyDst});
        
        ReadOnlySpan<byte> whitePixel = [255, 255, 255, 255];
        defaultWhiteTexture.Write(whitePixel, bytesPerRow: 4, rowsPerImage: 1, writeSize: new GpuExtent3D(1, 1, 1));
        
        defaultWhiteTextureView = new ImTextureView(defaultWhiteTexture.CreateView(), new Vector2(0.5f, 0.5f));
        */
    }
    
    public void Dispose()
    {
        gui.Dispose();
        // defaultWhiteTexture.Dispose();
        samplerLinear.Dispose();
        samplerNearest.Dispose();
        defaultFont.DisposeInternal();
    }
}
