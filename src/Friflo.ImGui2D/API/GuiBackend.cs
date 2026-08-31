// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global
// ReSharper disable once CheckNamespace
namespace Friflo.ImGui2D;


public abstract class ImBuffer<T> : IDisposable where T : unmanaged 
{
    public abstract void        Dispose();
    public abstract Memory<T>   Memory { get; }
    public abstract void        Write(int start, int length);
}

public abstract class ImGuiBackend : IDisposable
{
    private             ImFont?     defaultFont;
    public   readonly   GuiInput    input;
    internal readonly   GuiHost     host;
    
    public              ImFont      DefaultFont => defaultFont ??= CreateDefaultFont();

    protected internal abstract  ImTexture           CreateTexture(string name, int width, int height, ReadOnlySpan<byte> rgbaPixels);
    protected internal abstract  ImBuffer<Vertex2D>  CreateVertexBuffer(int vertexCount);
    protected internal abstract  ImBuffer<uint>      CreateIndexBuffer(int indexCount);
    
    protected ImGuiBackend()
    {
        input   = new GuiInput();
        host    = new GuiHost(input);
    }
    
    public void NewFrame()
    {
        foreach (var window in host.windowOrder) {
            window.NewFrame();
        }
        input.NewFrame();
    }

    public void AddEvent(in ImEvent ev) => input.AddEvent(ev);
    
    public virtual void Dispose()
    {
        defaultFont?.DisposeInternal();
        host.Dispose();
    }
    
    private ImFont CreateDefaultFont()
    {
        using var fontAtlas = typeof(ImGuiBackend).Assembly.GetManifestResourceStream("Friflo.ImGui2D.fonts.arial-48-latin_0.png");
        using var fntFile   = typeof(ImGuiBackend).Assembly.GetManifestResourceStream("Friflo.ImGui2D.fonts.arial-48-latin.fnt");
        using var reader    = new StreamReader(fntFile!, Encoding.UTF8);
        var fntContent      = reader.ReadToEnd();
        
        return ImFont.CreateBMFont(this, fntContent, fontAtlas!, "Default Font", false);
    }
    
    /// <summary> E.g. <c>device.CreateMonocraftFont(48, 256, 256, 32, 95, "Monocraft");</c> </summary>
    public ImFont CreateMonocraftFont(float fontSize, int width, int height, int firstChar, int charCount, string name)
    {
        using var ttfFont = typeof(ImGuiBackend).Assembly.GetManifestResourceStream("Friflo.ImGui2D.fonts.Monocraft.ttf")!;
        
        return ImFont.CreateTtfFont(this, ttfFont, fontSize, width, height, firstChar, charCount, name, true);
    }
    
    public ImFont CreateBMFont(ReadOnlySpan<char> fntContent, Stream fontAtlas, string name)
    {
        return ImFont.CreateBMFont(this, fntContent, fontAtlas, name, true);
    }
    
    public ImFont CreateTtfFont(Stream ttfStream, float fontSize, int width, int height, int firstChar, int charCount, string name)
    {
        return ImFont.CreateTtfFont(this, ttfStream, fontSize, width, height, firstChar, charCount, name, true);
    }
}