using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Friflo.SmGui;
using Friflo.SmGui.Session;
using Friflo.SmGui.TUI;
using NUnit.Framework;

// ReSharper disable InconsistentNaming
// ReSharper disable SuggestVarOrType_Elsewhere
// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable UnusedMember.Local
// ReSharper disable once InconsistentNaming
namespace Tests.SmGui;

public static class Tests_Gui_Misc
{
    private static int SizeOf(Type type)
    {
        int size = (int)typeof(Unsafe).GetMethod(nameof(Unsafe.SizeOf))!
            .MakeGenericMethod(type)
            .Invoke(null, null)!;
        return size;
    }
    
    [Test]
    public static void Tests_Gui_SizeOf()
    {
        var assembly =  typeof(SmDraw).Assembly;
        
        var LoopEvent = assembly.GetType("Friflo.SmGui.Session.LoopEvent")!;
        Assert.AreEqual( 16, Unsafe.SizeOf<Payload>());
        Assert.AreEqual( 32, SizeOf(LoopEvent));
        
        Assert.AreEqual( 12, Unsafe.SizeOf<Dim>());
        
        Assert.AreEqual( 32, Unsafe.SizeOf<SmTexture>());
        Assert.AreEqual(  8, Unsafe.SizeOf<MemoryView>());
        Assert.AreEqual( 16, Unsafe.SizeOf<RectVector2>());
        Assert.AreEqual(152, Unsafe.SizeOf<DrawCommand>());
        
        var TuiRect = assembly.GetType("Friflo.SmGui.TUI.TuiRect")!;
        
        Assert.AreEqual(  6, Unsafe.SizeOf<Color32Span>());
        Assert.AreEqual(  8, Unsafe.SizeOf<TextSpan>());
        Assert.AreEqual( 32, Marshal.SizeOf(TuiRect));
        Assert.AreEqual( 16, Unsafe.SizeOf<TuiColorCell>());
        
        Assert.AreEqual( 4,  Unsafe.SizeOf<Rune>());
        Assert.AreEqual(20,  Unsafe.SizeOf<Vertex2D>());
        Assert.AreEqual(80,  Unsafe.SizeOf<VertexQuad>());
        
        var WsDrawCommand = assembly.GetType("Friflo.SmGui.Session.WsDrawCommand")!;
        Assert.AreEqual(92,  Marshal.SizeOf(WsDrawCommand));
        Assert.AreEqual( 8,  Unsafe.SizeOf<SeqChange>());
        
        // --- SmEvent
        Assert.AreEqual( 32,  Unsafe.SizeOf<SmEvent>());
        Assert.AreEqual(  1,  Unsafe.SizeOf<SmEventType>());
        Assert.AreEqual(  8,  Unsafe.SizeOf<KeyEvent>());
        Assert.AreEqual(  2,  Unsafe.SizeOf<GamepadEvent>());
        
        Assert.AreEqual( 32,  Unsafe.SizeOf<GlyphInfo>());
    }
}