// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Numerics;

// ReSharper disable UseWithExpressionToCopyStruct
// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToConstant.Local
// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;


public readonly ref partial struct GuiWidget
{
#region child
    internal ChildScope BeginChild(WidgetID childId, Vector2 size)
    {
        var window = Window;

        var parentStartCursor = window.Cursor;
        window.PushScope(childId);

        var availableSize = window.size - (parentStartCursor - window.pos);
        
        var initialClipSize = new Vector2(
            size.X > 0f ? size.X : Math.Max(0f, availableSize.X),
            size.Y > 0f ? size.Y : Math.Max(0f, availableSize.Y)
        );
        draw.PushScissor(parentStartCursor, initialClipSize);
        
        window.SetCursor(parentStartCursor + new Vector2(5f, 5f)); // inner cursor + padding
        window.PushLayout(LayoutDirection.Vertical);
        return new ChildScope(this, parentStartCursor, size);
    }
    
    internal void EndChild(Vector2 parentStartCursor, Vector2 requestedSize)
    {
        var window = Window;
        var padding = new Vector2(5f, 5f);
        Vector2 contentSize = window.PopLayout(); // returns accumulated bounding box of inner widgets

        draw.PopScissor();
        window.PopScope();

        // Dynamic Auto-Fit: if requestedSize <= 0, use measured Content + Padding
        Vector2 finalChildSize = new Vector2(
            requestedSize.X > 0f ? requestedSize.X : contentSize.X + (padding.X * 2f),
            requestedSize.Y > 0f ? requestedSize.Y : contentSize.Y + (padding.Y * 2f)
        );
        window.SetCursor(parentStartCursor);
        window.MoveCursor(finalChildSize);
    }
#endregion


#region scroll area
    internal ScrollAreaScope BeginScrollArea(int childId, Vector2 size)
    {
        var window = Window;
        var parentStartCursor = window.Cursor;
        window.PushScope(childId);

        var availableSize = window.size - (parentStartCursor - window.pos);
        var finalSize = new Vector2(
            size.X > 0f ? size.X : Math.Max(0f, availableSize.X),
            size.Y > 0f ? size.Y : Math.Max(0f, availableSize.Y)
        );
        draw.PushScissor(parentStartCursor, finalSize); // Push scissor region for clipping

        ref var scrollState = ref window.GetOrCreateScrollState(childId);  // Retrieve or create persistent scroll state

        // Process mouse wheel input when hovering over the scroll region
        if (window.IsHoverAt(parentStartCursor, finalSize, draw)) {
            float wheel = input.MouseWheel.Y;
            if (wheel != 0f) {
                scrollState.offsetY -= wheel * LineHeight;
                scrollState.offsetY = Math.Max(0f, scrollState.offsetY); // Prevent negative offset
            }
        }
        // 4. Offset inner start cursor by current scroll position
        Vector2 innerPadding = new Vector2(5f, 5f);
        Vector2 innerStartCursor = parentStartCursor + innerPadding - new Vector2(0f, scrollState.offsetY);

        window.SetCursor(innerStartCursor);
        window.PushLayout(LayoutDirection.Vertical);

        // Reuse the ref struct ChildScope for zero-allocation scope handling
        return new ScrollAreaScope(this, childId, parentStartCursor, finalSize);
    }

    internal void EndScrollArea(int childId, Vector2 parentStartCursor, Vector2 childSize)
    {
        var window = Window;
        
        // Retrieve total measured content height
        var contentSize = window.PopLayout();
        draw.PopScissor();

        // Clamp scroll offset within valid bounds
        ref var scrollState = ref window.GetOrCreateScrollState(childId);
        float maxScroll     = Math.Max(0f, contentSize.Y - childSize.Y);
        scrollState.offsetY = Math.Clamp(scrollState.offsetY, 0f, maxScroll);

        // Render vertical scrollbar if content exceeds visible area
        if (contentSize.Y > childSize.Y) {
            DrawScrollbar(childId, parentStartCursor, childSize, contentSize.Y, ref scrollState);
        }
        window.PopScope();

        // Restore parent cursor and advance parent layout
        window.SetCursor(parentStartCursor);
        window.MoveCursor(childSize);
    }
    
    private void DrawScrollbar(int childId, Vector2 pos, Vector2 size, float totalContentHeight, ref ScrollState scrollState)
    {
        var window = Window;
        float trackWidth = 8f;
        Vector2 trackPos = new Vector2(pos.X + size.X - trackWidth, pos.Y);
        
        // Calculate thumb dimensions
        float visibleRatio          = size.Y / totalContentHeight;
        float thumbHeight           = Math.Max(20f, size.Y * visibleRatio);
        float scrollableRange       = totalContentHeight - size.Y;
        float thumbScrollableRange  = size.Y - thumbHeight;

        float thumbY        = (scrollState.offsetY / scrollableRange) * thumbScrollableRange;
        Vector2 thumbPos    = new Vector2(trackPos.X, trackPos.Y + thumbY);
        Vector2 thumbSize   = new Vector2(trackWidth, thumbHeight);

        // Hit testing
        bool isHovered = window.IsHoverAt(thumbPos, thumbSize, draw);
        // Handle mouse drag start
        if (isHovered && input.IsMouseDown && !scrollState.isDragging) {
            scrollState.isDragging = true;
            scrollState.dragStartMouseY = input.MousePos.Y;
            scrollState.dragStartOffsetY = scrollState.offsetY;
            input.SetActiveWidget(childId);
        }
        // Handle active mouse dragging
        if (scrollState.isDragging) {
            if (input.IsMouseDown) {
                float mouseDeltaY = input.MousePos.Y - scrollState.dragStartMouseY;
                float scrollDeltaY = (mouseDeltaY / thumbScrollableRange) * scrollableRange;
                scrollState.offsetY = Math.Clamp(scrollState.dragStartOffsetY + scrollDeltaY, 0f, scrollableRange);
            } else {
                scrollState.isDragging = false;
                input.SetActiveWidget(0);
            }
        }
        // Visual feedback on hover/drag
        Color32 thumbColor = scrollState.isDragging ? Color.ScrollThumbActive 
                             : isHovered            ? Color.ScrollThumbHover 
                                                    : Color.ScrollThumb;
        // Render track and thumb
        draw.FillRect(trackPos, new Vector2(trackWidth, size.Y), Color.ScrollTrackBg);
        draw.FillRectRounded(thumbPos, thumbSize, 3f, thumbColor);
    }
#endregion
}

