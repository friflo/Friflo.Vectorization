// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Numerics;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable once CheckNamespace
namespace Friflo.ImGui2D;


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
        return new HorizontalCenterScope(this, centerId, align, draw.batch.vertexCount, oldMouseOffset);
    }
    
    internal void EndHorizontalAligned(in HorizontalCenterScope scope)
    {
        var maxSize = PopLayout();
        
        input.mouseOffset   = scope.oldMouseOffset;
        var availableWidth  = Window.CurrentLayout.boundsSize.X;
        var offset          = (availableWidth - maxSize.X) * scope.align;
        var vertices        = draw.batch.vertexBuffer.Span.Slice(scope.vertexStart, draw.batch.vertexCount);
        
        foreach (ref var vertex in vertices) {
            vertex.position.X += offset;
        }
        guiState.mouseOffsets[scope.centerId] = new Vector2(offset, 0);
    }
}