// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Numerics;

// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable UseWithExpressionToCopyStruct
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;


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
	    if (scrollState.isHovered) {
		    input.actionHoverCaptured = true;
	    }
	    var dragState = scrollState.dragState = GetDragState(scrollState.isHovered, childId);
		ApplyScrollOffset(ref scrollState, dragState, outerSize);
	    
	    // Offset inner start cursor by current scroll position
	    var innerStartCursor = startCursor + padding.Min - scrollState.offset;

	    // Account for vertical scrollbar visibility based on last frame's content size
	    bool hasVertScrollbar = scrollState.lastContentSize.Y > outerSize.Y;
	    float scrollbarWidth  = hasVertScrollbar ? Sizes.TrackThickness.X : 0f;

	    // Provide concrete viewport width for UI.Fill_X elements (accounting for padding, focus clearance, and scrollbar)
	    float effectiveWidth  = MathF.Max(0f, outerSize.X - padding.Size.X - scrollbarWidth);
	    float effectiveHeight = MathF.Max(0f, outerSize.Y - padding.Size.Y);

	    // Exact width (effectiveWidth) and Content height (0f, Sizing.Content)
	    var boundsSize = new Vector2(effectiveWidth, effectiveHeight);
        
	    return new RectVector2(innerStartCursor, boundsSize);
    }
    
    /// Calculate scroll offset in 1 pass based on <see cref="ScrollState.lastContentSize"/>
    private void ApplyScrollOffset(ref ScrollState scrollState, DragState dragState, Vector2 size)
    {
		// Handle active mouse dragging for the active axis
		var range		= new ScrollRange(size, scrollState.lastContentSize, new Vector2(20, 20));
		var mousePos	= input.MousePos;
			    
	    if (scrollState.isDragging) {
	        if (dragState == DragState.Down) {
	            var mouseDelta		= mousePos - scrollState.dragStartMouse;
				if (scrollState.dragAxis == ScrollAxis.Horizontal)	mouseDelta.Y = 0;
				else												mouseDelta.X = 0;
	            var scrollDelta		= (mouseDelta / range.maxThumbTravel) * range.maxScroll;
	            scrollState.offset	= Vector2.Clamp(scrollState.dragStartOffset + scrollDelta, default, range.maxScroll);
	            draw.Tui?.SnapExtentToGrid(ref scrollState.offset);
	        } else {
	            scrollState.isDragging = false;
	        }
	        return;
	    }
	    // Handle click on track outside thumb: Page Left/Right or Page Up/Down
	    if (dragState != DragState.Down) {
		    return;
	    }
	    if (scrollState.horizontalBar.visible && scrollState.horizontalBar.track.Contains(mousePos)) {
		    if (mousePos.X < scrollState.horizontalBar.thumb.pos.X) scrollState.offset.X = MathF.Max(0f,                scrollState.offset.X - size.X);
		    if (mousePos.X > scrollState.horizontalBar.thumb.BR.X)  scrollState.offset.X = MathF.Min(range.maxScroll.X, scrollState.offset.X + size.X);
	    }
	    if (scrollState.verticalBar.visible && scrollState.verticalBar.track.Contains(mousePos)) {
		    if (mousePos.Y < scrollState.verticalBar.thumb.pos.Y)	scrollState.offset.Y = MathF.Max(0f,                scrollState.offset.Y - size.Y);
		    if (mousePos.Y > scrollState.verticalBar.thumb.BR.Y)	scrollState.offset.Y = MathF.Min(range.maxScroll.Y, scrollState.offset.Y + size.Y);
	    }
	    draw.Tui?.SnapExtentToGrid(ref scrollState.offset);
    } 
    
    private void PopScrollArea(int childId, Vector2 startCursor, Vector2 outerSize, Vector2 scrollSize, Color32 background, bool drawTack)
    {
	    var window = Window;
	    
	    var baseContentSize = scrollSize;

	    ref var scrollState = ref window.GetOrCreateScrollState(childId);

	    // Determine actual visibility decoupled from mutation
	    bool showVert  = baseContentSize.Y > outerSize.Y;
	    bool showHoriz = baseContentSize.X > outerSize.X;

	    // Build effective content size without cross-contaminating initial triggers
	    var contentSize = baseContentSize;
	    if (showVert)  contentSize.X += Sizes.TrackThickness.X;
	    if (showHoriz) contentSize.Y += Sizes.TrackThickness.Y;

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
	    scrollState.isHovered		= false;
	    scrollState.horizontalBar	= default;
	    scrollState.verticalBar		= default;
	    
	    var range = new ScrollRange(outerSize, contentSize, new Vector2(20, 20));
	    if (showVert) {
	        DrawScrollbar(startCursor, outerSize, range, ref scrollState, ScrollAxis.Vertical, background, 0, drawTack);
	    }
	    if (showHoriz) {
		    var distRight = showVert ? Sizes.TrackThickness.X : 0;	// leave space for vertical scroll bar
	        DrawScrollbar(startCursor, outerSize, range, ref scrollState, ScrollAxis.Horizontal, background, distRight, drawTack);
	    }
    }
    
	private void DrawScrollbar(
        Vector2         pos,
        Vector2         size,
        ScrollRange     range,
        ref ScrollState scrollState,
        ScrollAxis      axis,
        Color32         background,
        float           distRight,
        bool            drawTack)
	{
	    var window			= Window;
	    bool isHorizontal	= axis == ScrollAxis.Horizontal;

	    // Axis-parameterized geometry setup
	    Vector2 trackPos;	Vector2 trackSize;
	    Vector2 thumbPos;	Vector2 thumbSize;
	    
	    if (isHorizontal) {
		    float thumbOffsetX	= (scrollState.offset.X / range.maxScroll.X) * range.maxThumbTravel.X;
			var scrollbarHeight = Sizes.TrackThickness.Y;
		    trackPos	= new Vector2(pos.X,  pos.Y + size.Y - scrollbarHeight); 
			trackSize	= new Vector2(size.X - distRight, scrollbarHeight);
			thumbPos	= new Vector2(trackPos.X + thumbOffsetX, trackPos.Y);
			thumbSize	= new Vector2(range.thumbSize.X - distRight, scrollbarHeight);
			scrollState.horizontalBar = new ScrollBar(trackPos, trackSize, thumbPos, thumbSize);
	    } else {
		    float thumbOffsetY	= (scrollState.offset.Y / range.maxScroll.Y) * range.maxThumbTravel.Y;
		    var scrollbarWidth	= Sizes.TrackThickness.X;
		    trackPos	= new Vector2(pos.X + size.X - scrollbarWidth, pos.Y);
			trackSize	= new Vector2(scrollbarWidth, size.Y);
			thumbPos	= new Vector2(trackPos.X, trackPos.Y + thumbOffsetY);
			thumbSize	= new Vector2(scrollbarWidth, range.thumbSize.Y);
			scrollState.verticalBar   = new ScrollBar(trackPos, trackSize, thumbPos, thumbSize);
	    }

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
	        // Debug.WriteLine("Drag Started");
	    }
	    
	    // Visual feedback on hover/drag
	    bool isCurrentDragging = scrollState.isDragging && scrollState.dragAxis == axis;
	    Color32 thumbColor = isCurrentDragging ? Colors.ScrollThumbActive 
	                       : isThumbHovered    ? Colors.ScrollThumbHover 
	                                           : Colors.ScrollThumb;

	    // Render track and thumb
	    var tui = draw.Tui;
	    float	offset		= tui != null ? 0 : 2;
	    Vector2 posOffset	= isHorizontal ? new Vector2(0, offset) : new Vector2(offset, 0);
	    thumbPos		   += posOffset;
	    thumbSize		   -= 2 * posOffset;
	    
	    if (tui != null) {
			tui.DrawScrollbar(trackPos, drawTack ? trackSize : default, background, thumbPos, thumbSize, thumbColor, isHorizontal);
	    } else {
		    draw.FillRect       (trackPos, trackSize, background);
		    draw.FillRectRounded(thumbPos, thumbSize, Sizes.CornerRadius, thumbColor, GuiSizes.CornerSegments);
	    }
	}
}

