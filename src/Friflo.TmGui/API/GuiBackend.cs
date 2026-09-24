// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.IO;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;


public abstract class TmBuffer<T> : IDisposable where T : unmanaged 
{
    public abstract void        Dispose();
    public abstract Memory<T>   Memory { get; }
    public abstract void        Write(int start, int length);
}


/// <summary> Same flags as WGPU enum TextureUsage </summary>
[Flags]
public enum TmTextureUsage
{
  None                  = 0,
  CopySrc               = 1,
  CopyDst               = 2,
  TextureBinding        = 4,
  StorageBinding        = 8,
  RenderAttachment      = 16,
  TransientAttachment   = 32,
}


public abstract class TmGuiBackend : IDisposable
{
    private             TmFont?     defaultFont;
    protected readonly  IGuiAssets  assets;
    public    readonly  GuiInput    input;
    internal  readonly  GuiHost     host;
    internal            IGuiAssets  Assets => assets;
    
    public              TmFont      DefaultFont => defaultFont ??= assets.CreateDefaultFont(this);

    protected internal abstract  TmBuffer<Vertex2D>  CreateVertexBuffer(int vertexCount);
    protected internal abstract  TmBuffer<uint>      CreateIndexBuffer(int indexCount);
    
    public abstract TmTexture   CreateTexture(string name, int width, int height, ReadOnlySpan<byte> rgbaPixels);
    public abstract TmTexture   LoadTexture  (Stream stream, string? label = null, TmTextureUsage usage = TmTextureUsage.TextureBinding | TmTextureUsage.CopyDst);
    
    protected TmGuiBackend(IGuiAssets assets)
    {
        this.assets = assets;
        input       = new GuiInput();
        host        = new GuiHost(input);
    }
    
    protected void InitBatch(TmBatch batch)
    {
        batch.InitBatch();
        batch.guiState.SetDefaultStyle(batch);
    }
    
    public void NewFrame()
    {
        foreach (var window in host.windowOrder) {
            window.NewFrame();
        }
        input.NewFrame();
    }

    public void AddEvent(in TmEvent ev) => input.AddEvent(ev);
    
    public virtual void Dispose()
    {
        defaultFont?.DisposeInternal();
        host.Dispose();
    }
    
    public TmFont CreateBMFont(ReadOnlySpan<char> fntContent, Stream fontAtlas, string name)
    {
        var image = assets.LoadImage(fontAtlas, TmColorComponents.RedGreenBlueAlpha);
        return TmFont.CreateBMFont(this, fntContent, image, name, true);
    }
    
    /// <summary> E.g. <c>backend.CreateTtfFont(ttf, 48, 256, 256, 32, 95, "Monocraft");</c> </summary>
    public TmFont CreateTtfFont(Stream ttfStream, float fontSize, int width, int height, int firstChar, int charCount, string name)
    {
        TmFont.AssertTextureDimension(width, height);
        
        var alphaBitmapTarget = new byte[width * height];
        var asset = assets.LoadTrueTypeFont(ttfStream, fontSize, width, height, alphaBitmapTarget, firstChar, charCount);
        
        return TmFont.CreateTtfFont(this, asset, alphaBitmapTarget, fontSize, width, height, name, true);
    }
}