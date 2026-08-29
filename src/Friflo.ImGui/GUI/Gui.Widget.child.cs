// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

// ReSharper disable UseWithExpressionToCopyStruct
// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToConstant.Local
// ReSharper disable once CheckNamespace
namespace Friflo.ImGui;


public readonly ref partial struct GuiWidget
{
#region child
	/// <summary>
	/// Determines whether the given axis size represents auto-sizing (<c>0.0f</c> or <see cref="float.NaN"/>).
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool IsAuto(float value) => value == 0f || float.IsNaN(value);
	
	private Vector2 ChildOuterSize(Vector2 size, out bool hasScissor)
	{
	    ref readonly var layout = ref Window.CurrentLayout;
	    Vector2 remaining = Vector2.Max(Vector2.Zero, layout.boundsSize - (layout.cursor - layout.startCursor));

	    hasScissor = !IsAuto(size.X) || !IsAuto(size.Y);

	    float width = remaining.X;
	    if (size.X > 0f) {
	        width = size.X;
	    }
	    else if (size.X < 0f) {
	        width = MathF.Max(0f, remaining.X + size.X);
	    }

	    float height = remaining.Y;
	    if (size.Y > 0f) {
	        height = size.Y;
	    }
	    else if (size.Y < 0f) {
	        height = MathF.Max(0f, remaining.Y + size.Y);
	    }
	    return new Vector2(width, height);
	}

	internal ChildScope BeginChild(WidgetID childId, Vector2 size)
	{
	    var window = Window;
	    var parentStartCursor = window.Cursor;
	    window.PushScope(childId);

	    var padding = Sizes.ChildPadding;
	    var calculatedOuterSize = ChildOuterSize(size, out bool hasScissor);

	    if (hasScissor) {
	        draw.PushScissor(parentStartCursor, calculatedOuterSize);
	    }
	    window.SetCursor(parentStartCursor + padding.Min);
	    PushLayout(LayoutDirection.Vertical, padding.Shrink(calculatedOuterSize));

	    return new ChildScope(this, parentStartCursor, size, calculatedOuterSize);
	}

	internal void EndChild(in ChildScope scope)
	{
	    var window = Window;
	    var padding = Sizes.ChildPadding;
	    Vector2 contentSize = PopLayout();

	    bool hasScissor = !IsAuto(scope.requestedSize.X) || !IsAuto(scope.requestedSize.Y);
	    if (hasScissor) {
	        draw.PopScissor();
	    }
	    window.PopScope();

	    Vector2 finalChildSize = new Vector2(
	        IsAuto(scope.requestedSize.X) ? contentSize.X + padding.Size.X : scope.calculatedOuterSize.X,
	        IsAuto(scope.requestedSize.Y) ? contentSize.Y + padding.Size.Y : scope.calculatedOuterSize.Y
	    );
	    window.SetCursor(scope.parentStartCursor);
	    MoveCursor(finalChildSize);
	}
#endregion


#region scroll area
	internal ScrollAreaScope BeginScrollArea(int childId, Vector2 size)
	{
	    var window = Window;
	    var parentStartCursor = window.Cursor;
	    window.PushScope(childId);

	    var availableSize = window.Size - (parentStartCursor - window.Pos);
	    var finalSize = new Vector2(
	        size.X > 0f ? size.X : Math.Max(0f, availableSize.X),
	        size.Y > 0f ? size.Y : Math.Max(0f, availableSize.Y)
	    );
		draw.PushScissor(parentStartCursor, finalSize); // Push scissor region for clipping
		draw.FillRect(parentStartCursor, size, Colors.ScrollAreaColor);

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
	    PushLayout(LayoutDirection.Vertical, size - Sizes.WindowPadding.Size);

		// Reuse the ref struct ChildScope for zero-allocation scope handling
	    return new ScrollAreaScope(this, childId, parentStartCursor, finalSize);
	}

	internal void EndScrollArea(in ScrollAreaScope scope)
	{
	    var window = Window;
	    
		// Retrieve total measured content size
	    var contentSize = PopLayout();
	    draw.PopScissor();
	    
	    window.PopScrollAreaInfo();

	    // Clamp scroll offset within valid bounds
	    ref var scrollState = ref window.GetOrCreateScrollState(scope.childId);
	    Vector2 maxScroll = new Vector2(
	        Math.Max(0f, contentSize.X - scope.requestedSize.X),
	        Math.Max(0f, contentSize.Y - scope.requestedSize.Y)
	    );
	    scrollState.offset = Vector2.Clamp(scrollState.offset, Vector2.Zero, maxScroll);

		// Render scrollbars if content exceeds visible area
	    if (contentSize.Y > scope.requestedSize.Y) {
	        DrawScrollbar(scope.childId, scope.parentStartCursor, scope.requestedSize, contentSize.Y, ref scrollState, ScrollAxis.Vertical);
	    }
	    if (contentSize.X > scope.requestedSize.X) {
	        DrawScrollbar(scope.childId, scope.parentStartCursor, scope.requestedSize, contentSize.X, ref scrollState, ScrollAxis.Horizontal);
	    }

	    window.PopScope();

		// Restore parent cursor and advance parent layout
	    window.SetCursor(scope.parentStartCursor);
	    MoveCursor(scope.requestedSize);
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
	    bool canHover		= !input.IsDragActive;
	    bool isThumbHovered = canHover && window.IsHoverAt(thumbPos, thumbSize, draw);
	    bool isTrackHovered = canHover && window.IsHoverAt(trackPos, trackSize, draw);

	    // Handle mouse drag start on thumb
	    var dragState	= GetDragState(isTrackHovered, childId);
	    var isDown 		= dragState == DragState.Down;
	    
	    if (isThumbHovered && isDown && !scrollState.isDragging) {
	        scrollState.isDragging		= true;
	        scrollState.dragAxis		= axis;
	        scrollState.dragStartMouse	= input.MousePos;
	        scrollState.dragStartOffset = scrollState.offset;
	    }
	    // Handle click on track (Page Left/Right or Page Up/Down)
	    else if (isTrackHovered && !isThumbHovered && isDown && !scrollState.isDragging) {
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
	        if (isDown) {
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
	        }
	    }

	    // Visual feedback on hover/drag
	    bool isCurrentDragging = scrollState.isDragging && scrollState.dragAxis == axis;
	    Color32 thumbColor = isCurrentDragging ? Colors.ScrollThumbActive 
	                       : isThumbHovered    ? Colors.ScrollThumbHover 
	                                           : Colors.ScrollThumb;

	    // Render track and thumb
	    draw.FillRect(trackPos, trackSize, Colors.ScrollTrackBg);
	    draw.FillRectRounded(thumbPos, thumbSize, 3f, thumbColor);
	}
#endregion
}

