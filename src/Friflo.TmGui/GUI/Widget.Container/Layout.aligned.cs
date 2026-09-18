// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Numerics;
using System.Runtime.InteropServices;

// ReSharper disable InconsistentNaming
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;


public readonly ref partial struct GuiWidget
{
    internal HorizontalCenterScope BeginHorizontalAligned(int centerId, float align, Dim size)
    {
        AlignedBeginReplay.Record(Recorder, new AlignedBegin(centerId, align, size));
        
        var oldLayoutOffset  = input.layoutOffset;
        ref var layoutOffset = ref CollectionsMarshal.GetValueRefOrAddDefault(guiState.layoutOffsets, centerId, out _);
        input.layoutOffset   = layoutOffset.shift;
        draw.batch.layoutOffset = input.layoutOffset;
        
        var boundsSize = Window.WidgetSize(size, default);
        PushLayout(LayoutDirection.Horizontal, boundsSize);
        
        var tui = draw.Tui;
        layoutOffset.startIndex = tui == null ? draw.batch.vertexCount : tui.tuiRects.Count;
        return new HorizontalCenterScope(this, new HorizontalCenterEnd(centerId, align, oldLayoutOffset));
    }
    
    internal void EndHorizontalAligned(in HorizontalCenterScope scope)
    {
        AlignedEndReplay.Record(Recorder, scope.end, false);
            
        var maxSize = PopLayout();
        
        ref var layoutOffset = ref CollectionsMarshal.GetValueRefOrAddDefault(guiState.layoutOffsets, scope.end.centerId, out _);
        var startIndex       = layoutOffset.startIndex;
        
        draw.batch.layoutOffset = input.layoutOffset = scope.end.oldLayoutOffset;
        var availableWidth  = Window.CurrentLayout.boundsSize.X;
        var offset          = (availableWidth - maxSize.X) * scope.end.align;
        var tui             = draw.Tui;
        if (tui == null) {
            var vertices = draw.batch.vertexBuffer.Span.Slice(startIndex, draw.batch.vertexCount - startIndex);
            foreach (ref var vertex in vertices) {
                vertex.position.X += offset;
            }
        } else {
            var rects = CollectionsMarshal.AsSpan(tui.tuiRects);
            rects = rects.Slice(startIndex, rects.Length - startIndex);
            foreach (ref var vertex in rects) {
                vertex.TL.X += offset;
                vertex.BR.X += offset;
            }
        }
        layoutOffset = new LayoutOffset { shift = new Vector2(offset, 0) };
    }
}


// --------------------------------------------- Step-Rendering ---------------------------------------------
internal readonly record struct AlignedBegin(int centerId, float align, Dim size);


internal sealed class AlignedBeginReplay : CmdReplay<AlignedBegin>
{
    protected internal override void Replay(in Replay replay, int index)
    {
        var cmd = commands[index];
        var scope = replay.widget.BeginHorizontalAligned(cmd.centerId, cmd.align, cmd.size);
        AlignedEndReplay.Record(replay.recorder, scope.end, true);
    }
    
    internal static void Record(GuiRecorder? rec, AlignedBegin cmd)
    {
        rec?.Record<AlignedBeginReplay, AlignedBegin>(cmd, false);
    }
}

internal sealed class AlignedEndReplay : CmdReplay<HorizontalCenterEnd>
{
    protected internal override void Replay(in Replay replay, int index)
    {
        replay.PopStackEnd();
        replay.widget.EndHorizontalAligned(new HorizontalCenterScope(replay.widget, commands[index]));
    }
    
    internal static void Record(GuiRecorder? rec, HorizontalCenterEnd end, bool isPush)
    {
        rec?.Record<AlignedEndReplay, HorizontalCenterEnd>(end, isPush);
    }
}
