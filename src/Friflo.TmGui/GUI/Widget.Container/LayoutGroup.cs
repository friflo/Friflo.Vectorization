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
    internal VerticalScope BeginVertical(Dim size)
    {
        LayoutBeginReplay.Record(Recorder, new LayoutBegin(size, LayoutType.Vertical));
        
        var boundsSize = Window.WidgetSize(size, default);
        PushLayout(LayoutDirection.Vertical, boundsSize);
        return new VerticalScope(this);
    }

    internal void EndVertical()
    {
        LayoutEndReplay.Record(Recorder, LayoutType.Vertical, false);
        
        PopLayout();
    }

    internal HorizontalScope BeginHorizontal(Dim size)
    {
        LayoutBeginReplay.Record(Recorder, new LayoutBegin(size, LayoutType.Horizontal));
        
        var boundsSize = Window.WidgetSize(size, default);
        PushLayout(LayoutDirection.Horizontal, boundsSize);
        return new HorizontalScope(this);
    }
    internal Vector2 EndHorizontal()
    {
        LayoutEndReplay.Record(Recorder, LayoutType.Horizontal, false);
        return PopLayout();
    }


    internal HorizontalCenterScope BeginHorizontalAligned(int centerId, float align, Dim size)
    {
        var oldLayoutOffset = input.layoutOffset;
        guiState.layoutOffsets.TryGetValue(centerId, out input.layoutOffset);
        draw.batch.layoutOffset = input.layoutOffset;
        
        BeginHorizontal(size);
        var tui = draw.Tui;
        var startIndex = tui == null ? draw.batch.vertexCount : tui.tuiRects.Count;
        return new HorizontalCenterScope(this, new HorizontalCenterEnd(centerId, align, startIndex, oldLayoutOffset));
    }
    
    internal void EndHorizontalAligned(in HorizontalCenterScope scope)
    {
        var maxSize = EndHorizontal();
        
        draw.batch.layoutOffset = input.layoutOffset = scope.end.oldLayoutOffset;
        var availableWidth  = Window.CurrentLayout.boundsSize.X;
        var offset          = (availableWidth - maxSize.X) * scope.end.align;
        var tui             = draw.Tui;
        if (tui == null) {
            var vertices = draw.batch.vertexBuffer.Span.Slice(scope.end.startIndex, draw.batch.vertexCount - scope.end.startIndex);
            foreach (ref var vertex in vertices) {
                vertex.position.X += offset;
            }
        } else {
            var rects = CollectionsMarshal.AsSpan(tui.tuiRects);
            rects = rects.Slice(scope.end.startIndex, rects.Length - scope.end.startIndex);
            foreach (ref var vertex in rects) {
                vertex.TL.X += offset;
                vertex.BR.X += offset;
            }
        }
        guiState.layoutOffsets[scope.end.centerId] = new Vector2(offset, 0);
    }
}

internal enum LayoutType { Horizontal, Vertical }

internal readonly record struct LayoutBegin (Dim size, LayoutType type);


internal class LayoutBeginReplay : CmdReplay<LayoutBegin>
{
    protected internal override void Replay(in Replay replay, int index)
    {
        var cmd = commands[index];
        if (cmd.type == LayoutType.Horizontal) {
            replay.widget.BeginHorizontal(cmd.size);
        } else {
            replay.widget.BeginVertical(cmd.size);
        }
        LayoutEndReplay.Record(replay.recorder, cmd.type, true);
    }
    
    internal static void Record(GuiRecorder? rec, LayoutBegin cmd)
    {
        rec?.Record<LayoutBeginReplay, LayoutBegin>(cmd, false);
    }
}

internal class LayoutEndReplay : CmdReplay<LayoutType>
{
    protected internal override void Replay(in Replay replay, int index)
    {
        replay.PopStackEnd();
        if (commands[index] == LayoutType.Horizontal) {
            replay.widget.EndHorizontal();
        } else {
            replay.widget.EndVertical();
        }
    }
    
    internal static void Record(GuiRecorder? rec, LayoutType type, bool isPush)
    {
        rec?.Record<LayoutEndReplay, LayoutType>(type, isPush);
    }
}