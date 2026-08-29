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
	private static bool IsAutoFit(float value) => value == 0f || float.IsNaN(value);

	private Vector2 ChildOuterSize(Vector2 size, out Vector2 innerLayoutSize, out bool hasScissor)
	{
	    ref readonly var layout = ref Window.CurrentLayout;
	    Vector2 remaining = Vector2.Max(Vector2.Zero, layout.boundsSize - (layout.cursor - layout.startCursor));

	    hasScissor = !IsAutoFit(size.X) || !IsAutoFit(size.Y);

	    float width  = size.X > 0f ? size.X : (size.X < 0f ? MathF.Max(0f, remaining.X + size.X) : remaining.X);
	    float height = size.Y > 0f ? size.Y : (size.Y < 0f ? MathF.Max(0f, remaining.Y + size.Y) : remaining.Y);

	    // invariant: width & height are not NaN at this point
	    var padding = Sizes.ChildPadding;
	    innerLayoutSize = new Vector2(
	        MathF.Max(0f, width  - padding.Size.X),
	        MathF.Max(0f, height - padding.Size.Y)
	    );
	    return new Vector2(width, height);
	}

	internal ChildScope BeginChild(WidgetID childId, Vector2 size)
	{
	    var window = Window;
	    var parentStartCursor = window.Cursor;
	    window.PushScope(childId);

	    var calculatedOuterSize = ChildOuterSize(size, out Vector2 innerLayoutSize, out bool hasScissor);

	    if (hasScissor) {
	        draw.PushScissor(parentStartCursor, calculatedOuterSize);
	    }
	    window.SetCursor(parentStartCursor + Sizes.ChildPadding.Min);
	    PushLayout(LayoutDirection.Vertical, innerLayoutSize);

	    return new ChildScope(this, parentStartCursor, size, calculatedOuterSize);
	}

	internal void EndChild(in ChildScope scope)
	{
	    var window = Window;
	    var padding = Sizes.ChildPadding;
	    Vector2 contentSize = PopLayout();

	    bool hasScissor = !IsAutoFit(scope.requestedSize.X) || !IsAutoFit(scope.requestedSize.Y);
	    if (hasScissor) {
	        draw.PopScissor();
	    }
	    window.PopScope();

	    Vector2 finalChildSize = new Vector2(
	        IsAutoFit(scope.requestedSize.X) ? contentSize.X + padding.Size.X : scope.calculatedOuterSize.X,
	        IsAutoFit(scope.requestedSize.Y) ? contentSize.Y + padding.Size.Y : scope.calculatedOuterSize.Y
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

	    // Compute outer bounds (remaining area for NaN)
	    var calculatedOuterSize = ChildOuterSize(size, out _, out _);

	    // Scroll areas ALWAYS require scissor clipping against their calculated outer size
	    draw.PushScissor(parentStartCursor, calculatedOuterSize);
	    draw.FillRect(parentStartCursor, calculatedOuterSize, Colors.ScrollAreaColor);

	    window.PushScrollAreaInfo(childId, parentStartCursor, calculatedOuterSize);

	    ref var scrollState = ref window.GetOrCreateScrollState(childId);

	    // Process mouse wheel input
	    if (window.IsHoverAt(parentStartCursor, calculatedOuterSize, draw)) {
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
	    var     padding			 = Sizes.ChildPadding;
	    Vector2 innerStartCursor = parentStartCursor + padding.Min - scrollState.offset;

	    window.SetCursor(innerStartCursor);

	    // Provide concrete viewport width for UI.FillX elements (accounting for full horizontal padding in 1-Pass)
	    float effectiveWidth         = MathF.Max(0f, calculatedOuterSize.X - padding.Size.X);

	    // Set concrete width for horizontal alignment while keeping height infinite for vertical scrolling
	    PushLayout(LayoutDirection.Vertical, new Vector2(effectiveWidth, float.NaN));

	    return new ScrollAreaScope(this, childId, parentStartCursor, size, calculatedOuterSize);
	}

	internal void EndScrollArea(in ScrollAreaScope scope)
	{
	    var window = Window;
	    var padding = Sizes.ChildPadding;
	    
	    // Retrieve total measured content size and include padding for bottom/right margins
	    Vector2 rawContent = PopLayout();
	    Vector2 contentSize = rawContent + padding.Size;
	    
	    // Pop scissor unconditionally
	    draw.PopScissor();
	    
	    window.PopScrollAreaInfo();

	    // Clamp scroll offset within valid bounds using the content size (inclusive of padding)
	    ref var scrollState = ref window.GetOrCreateScrollState(scope.childId);
	    Vector2 boundsSize = scope.calculatedOuterSize;

	    Vector2 maxScroll = new Vector2(
	        Math.Max(0f, contentSize.X - boundsSize.X),
	        Math.Max(0f, contentSize.Y - boundsSize.Y)
	    );
	    scrollState.offset = Vector2.Clamp(scrollState.offset, Vector2.Zero, maxScroll);

	    // Render scrollbars whenever content exceeds bounds
	    if (contentSize.Y > boundsSize.Y) {
	        DrawScrollbar(scope.childId, scope.parentStartCursor, boundsSize, contentSize.Y, ref scrollState, ScrollAxis.Vertical);
	    }
	    if (contentSize.X > boundsSize.X) {
	        DrawScrollbar(scope.childId, scope.parentStartCursor, boundsSize, contentSize.X, ref scrollState, ScrollAxis.Horizontal);
	    }

	    window.PopScope();

	    // Advance parent layout considering potential auto-fit expansion
	    Vector2 finalChildSize = new Vector2(
	        IsAutoFit(scope.requestedSize.X) ? contentSize.X : scope.calculatedOuterSize.X,
	        IsAutoFit(scope.requestedSize.Y) ? contentSize.Y : scope.calculatedOuterSize.Y
	    );
	    window.SetCursor(scope.parentStartCursor);
	    MoveCursor(finalChildSize);
	}

	private void DrawScrollbar(int childId, Vector2 pos, Vector2 size, float totalContentSize, ref ScrollState scrollState, ScrollAxis axis)
	{
	    var window = Window;
	    float trackThickness = Sizes.TrackThickness;
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
	    draw.FillRectRounded(thumbPos, thumbSize, Sizes.CornerRadius, thumbColor);
	}
#endregion
}

