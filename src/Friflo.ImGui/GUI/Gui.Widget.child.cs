// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Numerics;

// ReSharper disable RedundantSwitchExpressionArms
// ReSharper disable UseWithExpressionToCopyStruct
// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToConstant.Local
// ReSharper disable once CheckNamespace
namespace Friflo.ImGui;


public readonly ref partial struct GuiWidget
{
#region child
	internal ChildScope BeginChild(WidgetID childId, Dim size)
	{
	    var window = Window;
	    var parentStartCursor = window.Cursor;
	    window.PushScope(childId);

	    var outerSize = window.WidgetSize(size, default);
	    if (size.IsBounded) {
	        draw.PushScissor(parentStartCursor, outerSize);
	    }
	    window.SetCursor(parentStartCursor + Sizes.ChildPadding.Min);
	    var innerLayoutSize	= Dim.Size(outerSize - Sizes.ChildPadding.Size);
	    PushLayout(LayoutDirection.Vertical, innerLayoutSize);

	    return new ChildScope(this, parentStartCursor, size, outerSize);
	}

	internal void EndChild(in ChildScope scope)
	{
		var window = Window;
	    var padding = Sizes.ChildPadding;
	    Vector2 contentSize = PopLayout();

	    if (scope.requestedSize.IsBounded) {
	        draw.PopScissor();
	    }
	    window.PopScope();

	    Vector2 finalChildSize = new Vector2(
	        scope.requestedSize.IsAutoWidth  ? contentSize.X + padding.Size.X : scope.calculatedOuterSize.X,
	        scope.requestedSize.IsAutoHeight ? contentSize.Y + padding.Size.Y : scope.calculatedOuterSize.Y
	    );
	    window.SetCursor(scope.parentStartCursor);
	    MoveCursor(finalChildSize);
	}
#endregion


#region scroll area
	internal ScrollAreaScope BeginScrollArea(int childId, Dim size)
	{
	    var window = Window;
	    var parentStartCursor = window.Cursor;
	    window.PushScope(childId);

	    // Compute outer bounds for the scroll area viewport
	    var outerSize = window.WidgetSize(size, default);

	    // Scroll areas ALWAYS require scissor clipping against their calculated outer size
	    draw.PushScissor(parentStartCursor, outerSize);
	    draw.FillRect(parentStartCursor, outerSize, Colors.ScrollAreaColor);

	    window.PushScrollAreaInfo(childId, parentStartCursor, outerSize);

	    ref var scrollState = ref window.GetOrCreateScrollState(childId);

	    // Process mouse wheel input
	    if (window.IsHoverAt(parentStartCursor, outerSize, draw)) {
	        float wheelY = input.MouseWheel.Y;
	        float wheelX = input.MouseWheel.X;

	        if (input.IsShiftDown && wheelY != 0f) {
	            wheelX = wheelY;
	            wheelY = 0f;
	        }
	        if (wheelY != 0f) {
	            scrollState.offset.Y = MathF.Max(0f, scrollState.offset.Y - wheelY * LineHeight);
	        }
	        if (wheelX != 0f) {
	            scrollState.offset.X = MathF.Max(0f, scrollState.offset.X - wheelX * LineHeight);
	        }
	    }

	    // Offset inner start cursor by current scroll position
	    var padding = Sizes.ChildPadding;
	    Vector2 innerStartCursor = parentStartCursor + padding.Min - scrollState.offset;

	    window.SetCursor(innerStartCursor);

	    // Account for vertical scrollbar visibility based on last frame's content size
	    bool hasVertScrollbar = scrollState.lastContentSize.Y > outerSize.Y;
	    float scrollbarWidth  = hasVertScrollbar ? Sizes.TrackThickness : 0f;

	    // Provide concrete viewport width for UI.FillX elements (accounting for padding, focus clearance, and scrollbar)
	    float effectiveWidth = MathF.Max(0f, outerSize.X - padding.Size.X - 2 * Sizes.FocusOutlineThickness.X - scrollbarWidth);

	    // Exact width (effectiveWidth) and Content height (0f, Sizing.Content)
	    PushLayout(LayoutDirection.Vertical, Dim.Size(effectiveWidth, Fit.Content));

	    return new ScrollAreaScope(this, childId, parentStartCursor, size, outerSize);
	}

	internal void EndScrollArea(in ScrollAreaScope scope)
	{
	    var window = Window;
	    var padding = Sizes.ChildPadding;
	    Vector2 boundsSize = scope.calculatedOuterSize;
	    
	    // Measure base content including padding and focus outline clearance
	    Vector2 rawContent = PopLayout();
	    Vector2 baseContentSize = rawContent + padding.Size + 2 * Sizes.FocusOutlineThickness;

	    ref var scrollState = ref window.GetOrCreateScrollState(scope.childId);

	    // Determine actual visibility decoupled from mutation
	    bool showVert  = baseContentSize.Y > boundsSize.Y;
	    bool showHoriz = baseContentSize.X > boundsSize.X;

	    // Build effective content size without cross-contaminating initial triggers
	    Vector2 contentSize = baseContentSize;
	    if (showVert)  contentSize.X += Sizes.TrackThickness;
	    if (showHoriz) contentSize.Y += Sizes.TrackThickness;

	    // Cache current content size for the next frame's layout pass
	    scrollState.lastContentSize = contentSize;

	    // Pop scissor unconditionally
	    draw.PopScissor();
	    
	    window.PopScrollAreaInfo();

	    // Clamp scroll offset within valid bounds
	    Vector2 maxScroll = new Vector2(
	        MathF.Max(0f, contentSize.X - boundsSize.X),
	        MathF.Max(0f, contentSize.Y - boundsSize.Y)
	    );
	    scrollState.offset = Vector2.Clamp(scrollState.offset, Vector2.Zero, maxScroll);

	    // Render scrollbars based on exact visibility criteria
	    if (showVert) {
	        DrawScrollbar(scope.childId, scope.parentStartCursor, boundsSize, contentSize.Y, ref scrollState, ScrollAxis.Vertical);
	    }
	    if (showHoriz) {
	        DrawScrollbar(scope.childId, scope.parentStartCursor, boundsSize, contentSize.X, ref scrollState, ScrollAxis.Horizontal);
	    }

	    window.PopScope();

	    // Advance parent layout consistently with EndChild auto-sizing semantics
	    Vector2 finalChildSize = new Vector2(
	        scope.requestedSize.IsAutoWidth  ? contentSize.X : scope.calculatedOuterSize.X,
	        scope.requestedSize.IsAutoHeight ? contentSize.Y : scope.calculatedOuterSize.Y
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

