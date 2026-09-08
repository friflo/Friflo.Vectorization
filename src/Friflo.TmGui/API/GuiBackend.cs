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

public abstract class TmGuiBackend : IDisposable
{
    private             TmFont?     defaultFont;
    protected readonly  IGuiAssets  assets;
    public    readonly  GuiInput    input;
    internal  readonly  GuiHost     host;
    
    public              TmFont      DefaultFont => defaultFont ??= assets.CreateDefaultFont(this);

    protected internal abstract  TmTexture           CreateTexture(string name, int width, int height, ReadOnlySpan<byte> rgbaPixels);
    protected internal abstract  TmBuffer<Vertex2D>  CreateVertexBuffer(int vertexCount);
    protected internal abstract  TmBuffer<uint>      CreateIndexBuffer(int indexCount);
    
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
    
    /// <summary> E.g. <c>device.CreateMonocraftFont(48, 256, 256, 32, 95, "Monocraft");</c> </summary>
    public TmFont CreateMonocraftFont(float fontSize, int width, int height, int firstChar, int charCount, string name)
    {
        return assets.CreateMonocraftFont(this, fontSize, width, height, firstChar, charCount, name);
    }
    
    public TmFont CreateBMFont(ReadOnlySpan<char> fntContent, Stream fontAtlas, string name)
    {
        var image = assets.LoadImage(fontAtlas, TmColorComponents.RedGreenBlueAlpha);
        return TmFont.CreateBMFont(this, fntContent, image, name, true);
    }
    
    public TmFont CreateTtfFont(Stream ttfStream, float fontSize, int width, int height, int firstChar, int charCount, string name)
    {
        TmFont.AssertTextureDimension(width, height);
        
        var alphaBitmapTarget = new byte[width * height];
        var asset = assets.LoadTrueTypeFontAsset(ttfStream, fontSize, width, height, alphaBitmapTarget, firstChar, charCount);
        
        return TmFont.CreateTtfFont(this, asset, alphaBitmapTarget, fontSize, width, height, name, true);
    }
}