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

	    window.PushScrollAreaInfo(childId, parentStartCursor, finalSize);

	    ref var scrollState = ref window.GetOrCreateScrollState(childId);  // Retrieve or create persistent scroll state

	    // Process mouse wheel input (Vertical by default, Horizontal with Shift)
	    if (window.IsHoverAt(parentStartCursor, finalSize, draw)) {
	        float wheelY = input.MouseWheel.Y;
	        float wheelX = input.MouseWheel.X;

	        if (input.IsShiftDown && wheelY != 0f) {
	            wheelX = wheelY;
	            wheelY = 0f;
	        }
	        if (wheelY != 0f) {
	            scrollState.offset.Y = Math.Max(0f, scrollState.offset.Y - wheelY * LineHeight);
	        }
	        if (wheelX != 0f) {
	            scrollState.offset.X = Math.Max(0f, scrollState.offset.X - wheelX * LineHeight);
	        }
	    }

		// Offset inner start cursor by current scroll position
	    Vector2 innerPadding = new Vector2(5f, 5f);
	    Vector2 innerStartCursor = parentStartCursor + innerPadding - scrollState.offset;

	    window.SetCursor(innerStartCursor);
	    window.PushLayout(LayoutDirection.Vertical);

		// Reuse the ref struct ChildScope for zero-allocation scope handling
	    return new ScrollAreaScope(this, childId, parentStartCursor, finalSize);
	}

	internal void EndScrollArea(int childId, Vector2 parentStartCursor, Vector2 childSize)
	{
	    var window = Window;
	    
		// Retrieve total measured content size
	    var contentSize = window.PopLayout();
	    draw.PopScissor();
	    
	    window.PopScrollAreaInfo();

	    // Clamp scroll offset within valid bounds
	    ref var scrollState = ref window.GetOrCreateScrollState(childId);
	    Vector2 maxScroll = new Vector2(
	        Math.Max(0f, contentSize.X - childSize.X),
	        Math.Max(0f, contentSize.Y - childSize.Y)
	    );
	    scrollState.offset = Vector2.Clamp(scrollState.offset, Vector2.Zero, maxScroll);

		// Render scrollbars if content exceeds visible area
	    if (contentSize.Y > childSize.Y) {
	        DrawScrollbar(childId, parentStartCursor, childSize, contentSize.Y, ref scrollState, ScrollAxis.Vertical);
	    }
	    if (contentSize.X > childSize.X) {
	        DrawScrollbar(childId, parentStartCursor, childSize, contentSize.X, ref scrollState, ScrollAxis.Horizontal);
	    }

	    window.PopScope();

		// Restore parent cursor and advance parent layout
	    window.SetCursor(parentStartCursor);
	    window.MoveCursor(childSize);
	}

	private void DrawScrollbar(int childId, Vector2 pos, Vector2 size, float totalContentSize, ref ScrollState scrollState, ScrollAxis axis)
	{
	    var window = Window;
	    float trackThickness = 8f;
	    bool isHorizontal = axis == ScrollAxis.Horizontal;

	    // Axis-parameterized geometry setup
	    Vector2 trackPos = isHorizontal 
	        ? new Vector2(pos.X, pos.Y + size.Y - trackThickness) 
	        : new Vector2(pos.X + size.X - trackThickness, pos.Y);

	    Vector2 trackSize = isHorizontal 
	        ? new Vector2(size.X, trackThickness) 
	        : new Vector2(trackThickness, size.Y);

	    float viewLength			= isHorizontal ? size.X : size.Y;
	    float visibleRatio			= viewLength / totalContentSize;
	    float thumbLength			= Math.Max(20f, viewLength * visibleRatio);
	    float scrollableRange		= totalContentSize - viewLength;
	    float thumbScrollableRange	= viewLength - thumbLength;

	    float currentOffset	= isHorizontal ? scrollState.offset.X : scrollState.offset.Y;
	    float thumbOffset	= (currentOffset / scrollableRange) * thumbScrollableRange;

	    Vector2 thumbPos = isHorizontal 
	        ? new Vector2(trackPos.X + thumbOffset, trackPos.Y) 
	        : new Vector2(trackPos.X, trackPos.Y + thumbOffset);

	    Vector2 thumbSize = isHorizontal 
	        ? new Vector2(thumbLength, trackThickness) 
	        : new Vector2(trackThickness, thumbLength);

	    // Hit testing
	    bool isThumbHovered = window.IsHoverAt(thumbPos, thumbSize, draw);
	    bool isTrackHovered = window.IsHoverAt(trackPos, trackSize, draw);

	    // Handle mouse drag start on thumb
	    if (isThumbHovered && input.IsMouseDown && !scrollState.isDragging) {
	        scrollState.isDragging		= true;
	        scrollState.dragAxis		= axis;
	        scrollState.dragStartMouse	= input.MousePos;
	        scrollState.dragStartOffset = scrollState.offset;
	        input.SetActiveWidget(childId);
	    }
	    // Handle click on track (Page Left/Right or Page Up/Down)
	    else if (isTrackHovered && !isThumbHovered && input.IsMouseClicked && !scrollState.isDragging) {
	        float clickPos = isHorizontal ? (input.MousePos.X - trackPos.X) : (input.MousePos.Y - trackPos.Y);
	        if (clickPos < thumbOffset) {
	            if (isHorizontal) scrollState.offset.X = Math.Max(0f, scrollState.offset.X - size.X);
	            else              scrollState.offset.Y = Math.Max(0f, scrollState.offset.Y - size.Y);
	        } else if (clickPos > thumbOffset + thumbLength) {
	            if (isHorizontal) scrollState.offset.X = Math.Min(scrollableRange, scrollState.offset.X + size.X);
	            else              scrollState.offset.Y = Math.Min(scrollableRange, scrollState.offset.Y + size.Y);
	        }
	    }

	    // Handle active mouse dragging for the active axis
	    if (scrollState.isDragging && scrollState.dragAxis == axis) {
	        if (input.IsMouseDown) {
	            float mouseDelta = isHorizontal 
	                ? (input.MousePos.X - scrollState.dragStartMouse.X) 
	                : (input.MousePos.Y - scrollState.dragStartMouse.Y);

	            float scrollDelta = (mouseDelta / thumbScrollableRange) * scrollableRange;

	            if (isHorizontal) {
	                scrollState.offset.X = Math.Clamp(scrollState.dragStartOffset.X + scrollDelta, 0f, scrollableRange);
	            } else {
	                scrollState.offset.Y = Math.Clamp(scrollState.dragStartOffset.Y + scrollDelta, 0f, scrollableRange);
	            }
	        } else {
	            scrollState.isDragging = false;
	            input.SetActiveWidget(0);
	        }
	    }

	    // Visual feedback on hover/drag
	    bool isCurrentDragging = scrollState.isDragging && scrollState.dragAxis == axis;
	    Color32 thumbColor = isCurrentDragging ? Color.ScrollThumbActive 
	                       : isThumbHovered    ? Color.ScrollThumbHover 
	                                           : Color.ScrollThumb;

	    // Render track and thumb
	    draw.FillRect(trackPos, trackSize, Color.ScrollTrackBg);
	    draw.FillRectRounded(thumbPos, thumbSize, 3f, thumbColor);
	}
#endregion
}

