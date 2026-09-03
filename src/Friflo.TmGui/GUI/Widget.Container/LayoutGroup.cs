// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Numerics;
using System.Runtime.InteropServices;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;


public readonly ref partial struct GuiWidget
{
    internal VerticalScope BeginVertical(Dim size)
    {
        var boundsSize = Window.WidgetSize(size, default);
        PushLayout(LayoutDirection.Vertical, boundsSize);
        return new VerticalScope(this);
    }

    internal void EndVertical() => PopLayout();
    
    internal HorizontalScope BeginHorizontal(Dim size)
    {
        var boundsSize = Window.WidgetSize(size, default);
        PushLayout(LayoutDirection.Horizontal, boundsSize);
        return new HorizontalScope(this);
    }
    internal void EndHorizontal() => PopLayout();
    
    
    internal HorizontalCenterScope BeginHorizontalAligned(int centerId, float align, Dim size)
    {
        var oldMouseOffset = input.mouseOffset;
        guiState.mouseOffsets.TryGetValue(centerId, out input.mouseOffset);
        
        BeginHorizontal(size);
        var tui = draw.Tui;
        var startIndex = tui == null ? draw.batch.vertexCount : tui.tuiRects.Count;
        return new HorizontalCenterScope(this, centerId, align, startIndex, oldMouseOffset);
    }
    
    internal void EndHorizontalAligned(in HorizontalCenterScope scope)
    {
        var maxSize = PopLayout();
        
        input.mouseOffset   = scope.oldMouseOffset;
        var availableWidth  = Window.CurrentLayout.boundsSize.X;
        var offset          = (availableWidth - maxSize.X) * scope.align;
        var tui             = draw.Tui;
        if (tui == null) {
            var vertices = draw.batch.vertexBuffer.Span.Slice(scope.startIndex, draw.batch.vertexCount - scope.startIndex);
            foreach (ref var vertex in vertices) {
                vertex.position.X += offset;
            }
        } else {
            var rects = CollectionsMarshal.AsSpan(tui.tuiRects);
            rects = rects.Slice(scope.startIndex, rects.Length - scope.startIndex);
            foreach (ref var vertex in rects) {
                vertex.TL.X += offset;
                vertex.BR.X += offset;
            }
        }
        guiState.mouseOffsets[scope.centerId] = new Vector2(offset, 0);
    }
}