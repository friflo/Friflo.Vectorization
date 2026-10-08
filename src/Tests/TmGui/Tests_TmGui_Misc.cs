using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Friflo.TmGui;
using Friflo.TmGui.Session;
using Friflo.TmGui.TUI;
using NUnit.Framework;

// ReSharper disable InconsistentNaming
// ReSharper disable SuggestVarOrType_Elsewhere
// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable UnusedMember.Local
// ReSharper disable once InconsistentNaming
namespace Tests.TmGui;

public static class Tests_TmGui_Misc
{
    [Test]
    public static void Tests_TmGui_SizeOf()
    {
        var assembly =  typeof(TmDraw).Assembly;
        
        Assert.AreEqual( 12, Unsafe.SizeOf<Dim>());
        
        Assert.AreEqual( 32, Unsafe.SizeOf<TmTexture>());
        Assert.AreEqual(  8, Unsafe.SizeOf<MemoryView>());
        Assert.AreEqual( 16, Unsafe.SizeOf<RectVector2>());
        Assert.AreEqual(152, Unsafe.SizeOf<DrawCommand>());
        
        var TuiRect = assembly.GetType("Friflo.TmGui.TUI.TuiRect")!;
        
        Assert.AreEqual(  6, Unsafe.SizeOf<Color32Span>());
        Assert.AreEqual(  8, Unsafe.SizeOf<TextSpan>());
        Assert.AreEqual( 32, Marshal.SizeOf(TuiRect));
        Assert.AreEqual( 16, Unsafe.SizeOf<TuiColorCell>());
        
        Assert.AreEqual( 4,  Unsafe.SizeOf<Rune>());
        Assert.AreEqual(20,  Unsafe.SizeOf<Vertex2D>());
        Assert.AreEqual(80,  Unsafe.SizeOf<VertexQuad>());
        
        var WsDrawCommand = assembly.GetType("Friflo.TmGui.Session.WsDrawCommand")!;
        Assert.AreEqual(92,  Marshal.SizeOf(WsDrawCommand));
        Assert.AreEqual( 8,  Unsafe.SizeOf<SeqChange>());
    }
}