// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;

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
    public   readonly   GuiInput    input;
    internal readonly   GuiHost     host;
    
    public              TmFont      DefaultFont => defaultFont ??= CreateDefaultFont();

    protected internal abstract  TmTexture           CreateTexture(string name, int width, int height, ReadOnlySpan<byte> rgbaPixels);
    protected internal abstract  TmBuffer<Vertex2D>  CreateVertexBuffer(int vertexCount);
    protected internal abstract  TmBuffer<uint>      CreateIndexBuffer(int indexCount);
    
    protected TmGuiBackend()
    {
        input   = new GuiInput();
        host    = new GuiHost(input);
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
    
    private TmFont CreateDefaultFont()
    {
        using var fontAtlas = typeof(TmGuiBackend).Assembly.GetManifestResourceStream("Friflo.ImGui2D.fonts.arial-48-latin_0.png");
        using var fntFile   = typeof(TmGuiBackend).Assembly.GetManifestResourceStream("Friflo.ImGui2D.fonts.arial-48-latin.fnt");
        using var reader    = new StreamReader(fntFile!, Encoding.UTF8);
        var fntContent      = reader.ReadToEnd();
        
        return TmFont.CreateBMFont(this, fntContent, fontAtlas!, "Default Font", false);
    }
    
    /// <summary> E.g. <c>device.CreateMonocraftFont(48, 256, 256, 32, 95, "Monocraft");</c> </summary>
    public TmFont CreateMonocraftFont(float fontSize, int width, int height, int firstChar, int charCount, string name)
    {
        using var ttfFont = typeof(TmGuiBackend).Assembly.GetManifestResourceStream("Friflo.ImGui2D.fonts.Monocraft.ttf")!;
        
        return TmFont.CreateTtfFont(this, ttfFont, fontSize, width, height, firstChar, charCount, name, true);
    }
    
    public TmFont CreateBMFont(ReadOnlySpan<char> fntContent, Stream fontAtlas, string name)
    {
        return TmFont.CreateBMFont(this, fntContent, fontAtlas, name, true);
    }
    
    public TmFont CreateTtfFont(Stream ttfStream, float fontSize, int width, int height, int firstChar, int charCount, string name)
    {
        return TmFont.CreateTtfFont(this, ttfStream, fontSize, width, height, firstChar, charCount, name, true);
    }
}