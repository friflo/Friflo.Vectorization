// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Numerics;

// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable UseWithExpressionToCopyStruct
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable once CheckNamespace
namespace Friflo.ImGui2D;


public readonly ref partial struct GuiWidget
{
    private RectVector2 PushScrollArea(int childId, Vector2 startCursor, Vector2 outerSize, Padding2D padding)
    {
	    var window = Window;
	    window.PushScrollAreaInfo(childId, startCursor, outerSize);

	    ref var scrollState = ref window.GetOrCreateScrollState(childId);

	    // Process mouse wheel input
	    if (window.IsHoverAt(startCursor, outerSize, draw)) {
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
	    scrollState.dragState = GetDragState(scrollState.isHovered, childId);
	    
	    // Offset inner start cursor by current scroll position
	    var innerStartCursor = startCursor + padding.Min - scrollState.offset;

	    // Account for vertical scrollbar visibility based on last frame's content size
	    bool hasVertScrollbar = scrollState.lastContentSize.Y > outerSize.Y;
	    float scrollbarWidth  = hasVertScrollbar ? Sizes.TrackThickness : 0f;

	    // Provide concrete viewport width for UI.Fill_X elements (accounting for padding, focus clearance, and scrollbar)
	    float effectiveWidth  = MathF.Max(0f, outerSize.X - padding.Size.X - scrollbarWidth);
	    float effectiveHeight = MathF.Max(0f, outerSize.Y - padding.Size.Y);

	    // Exact width (effectiveWidth) and Content height (0f, Sizing.Content)
	    var boundsSize = new Vector2(effectiveWidth, effectiveHeight);
        
	    return new RectVector2(innerStartCursor, boundsSize);
    }
    
    private void PopScrollArea(int childId, Vector2 startCursor, Vector2 outerSize, Vector2 scrollSize, Color32 background)
    {
	    var window = Window;
	    
	    var baseContentSize = scrollSize;

	    ref var scrollState = ref window.GetOrCreateScrollState(childId);

	    // Determine actual visibility decoupled from mutation
	    bool showVert  = baseContentSize.Y > outerSize.Y;
	    bool showHoriz = baseContentSize.X > outerSize.X;

	    // Build effective content size without cross-contaminating initial triggers
	    var contentSize = baseContentSize;
	    if (showVert)  contentSize.X += Sizes.TrackThickness;
	    if (showHoriz) contentSize.Y += Sizes.TrackThickness;

	    // Cache current content size for the next frame's layout pass
	    scrollState.lastContentSize = contentSize;

	    
	    window.PopScrollAreaInfo();

	    // Clamp scroll offset within valid bounds
	    var maxScroll = new Vector2(
	        MathF.Max(0f, contentSize.X - outerSize.X),
	        MathF.Max(0f, contentSize.Y - outerSize.Y)
	    );
	    scrollState.offset = Vector2.Clamp(scrollState.offset, Vector2.Zero, maxScroll);

	    // Render scrollbars based on exact visibility criteria
	    scrollState.isHovered = false;
	    if (showVert) {
	        DrawScrollbar(startCursor, outerSize, contentSize.Y, ref scrollState, ScrollAxis.Vertical, background);
	    }
	    if (showHoriz) {
	        DrawScrollbar(startCursor, outerSize, contentSize.X, ref scrollState, ScrollAxis.Horizontal, background);
	    }
    }
    
	private void DrawScrollbar(Vector2 pos, Vector2 size, float totalContentSize, ref ScrollState scrollState, ScrollAxis axis, Color32 background)
	{
	    var window = Window;
	    float trackThickness	= Sizes.TrackThickness;
	    bool isHorizontal = axis == ScrollAxis.Horizontal;

	    // Axis-parameterized geometry setup
	    Vector2 trackPos = isHorizontal 
	        ? new Vector2(pos.X,                           pos.Y + size.Y - trackThickness) 
	        : new Vector2(pos.X + size.X - trackThickness, pos.Y);

	    Vector2 trackSize = isHorizontal 
	        ? new Vector2(size.X, trackThickness) 
	        : new Vector2(trackThickness, size.Y);

	    float viewLength			= isHorizontal ? size.X : size.Y;
	    float visibleRatio			= viewLength / totalContentSize;
	    float thumbLength			= MathF.Max(20f, viewLength * visibleRatio);
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
		bool isDown			= scrollState.dragState == DragState.Down;
	    bool canHover		= !input.IsDragActive || isDown;
	    bool isThumbHovered = canHover && window.IsHoverAt(thumbPos, thumbSize, draw);
	    bool isTrackHovered = canHover && window.IsHoverAt(trackPos, trackSize, draw);
		if (isTrackHovered) {
			scrollState.isHovered = true;
	    }
		
	    // Handle mouse drag start on thumb
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
	            if (isHorizontal) scrollState.offset.X = MathF.Max(0f, scrollState.offset.X - size.X);
	            else              scrollState.offset.Y = MathF.Max(0f, scrollState.offset.Y - size.Y);
	        } else if (clickPos > thumbOffset + thumbLength) {
	            if (isHorizontal) scrollState.offset.X = MathF.Min(scrollableRange, scrollState.offset.X + size.X);
	            else              scrollState.offset.Y = MathF.Min(scrollableRange, scrollState.offset.Y + size.Y);
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
	    draw.FillRect       (trackPos, trackSize, background);
		float offset			= 2;
	    Vector2 posOffset = isHorizontal ? new Vector2(0, offset) : new Vector2(offset, 0);
	    draw.FillRectRounded(thumbPos + posOffset, thumbSize - 2 * posOffset, Sizes.CornerRadius, thumbColor, GuiSizes.CornerSegments);
	}
}

