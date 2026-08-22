// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

// ReSharper disable ConvertIfStatementToReturnStatement
// ReSharper disable InconsistentNaming
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UseWithExpressionToCopyStruct
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;


public readonly ref struct GuiWidget
{
    public  readonly    Draw2D          draw;           // 16 bytes
    public  readonly    GuiInput        input;          //  8 bytes
    private readonly    GuiState        guiState;       //  8 bytes
    private readonly    GuiStyle        currentStyle;   //  8 bytes
    
    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public ref readonly GuiColor        Color           { [DebuggerStepThrough] get => ref currentStyle.color; }
    public              GuiWindow       Window          { [DebuggerStepThrough] get => guiState.window; }
    public              float           LineHeight      { [DebuggerStepThrough] get => draw.DefaultFont.lineHeight; }
    public              IFormatProvider FormatProvider  { [DebuggerStepThrough] get => draw.batch.formatProvider; }
    public              bool            IsSet           { [DebuggerStepThrough] get => currentStyle != null; }

    
    /// <summary> Clears and returns a cached <see cref="System.Text.StringBuilder"/> to prevent allocations. </summary>
    public              StringBuilder   StringBuilder() => draw.batch.StringBuilder();

    public bool RegisterFocusable(int widgetId, in Vector2 center, out bool gainedFocus)
    {
        if (guiState.IsNewFrame) {
            return input.RegisterFocusable(widgetId, center, out gainedFocus);
        }
        gainedFocus = false;
        return false;
    }
    
    public WidgetState GetWidgetState(bool isHover, int widgetId)
    {
        if (guiState.IsNewFrame) {
            return input.GetWidgetState(isHover, widgetId);    
        }
        return WidgetState.None;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsFired(WidgetState widgetState, bool isFocused) {
        return widgetState == WidgetState.Clicked || (isFocused && input.IsSubmitPressed);
    }
    
    public StyleScope UseStyle(GuiStyle? style)
    {
        if (style is null) return default;
        PushStyle(style);
        return new StyleScope(this);
    }
    
    internal GuiWidget(Draw2D draw, Batch2D batch) {
        this.draw       = draw;
        input           = batch.input;
        guiState        = batch.guiState;
        guiState.SetFrameCount(input.FrameCount);
        currentStyle    = guiState.currentStyle;
    }
#region Window
    internal WindowScope BeginWindow(string title, Vector2? pos, Vector2? size)
    {
        var host = draw.batch.host;
        if (!host.windows.TryGetValue(title, out guiState.window!)) {
            guiState.window = new GuiWindow(host, title) {
                pos     = pos  ?? new Vector2( 50,  50),
                size    = size ?? new Vector2(300, 200)
            };
            host.windows.Add(title, guiState.window);
            host.windowOrder.Add(guiState.window);
        }
        var window      = Window;
        
        // Hit test whole window
        bool isWindowHovered = window.IsHoverAt(window.pos, window.size, draw);

        // Focus window on click (WITHOUT capturing activeItem)
        if (isWindowHovered && input.IsMouseDown) {
            // Note: Moving window to front here ensures that subsequent child widgets 
            //       in this same frame pass the IsTopWindowAt() check and process clicks immediately.
            host.FocusWindow(window);
        }
        draw.PushZIndex(host.windowOrder.IndexOf(window) + 1); // +1, so 0 is background;
        
        window.ResetScope();
        int parentHash = window.GetCurrentScopeHash();

        // Process window resize
        int resizeId 	= WidgetID.CombineHash(parentHash, "__resize".GetHashCode());
        bool isResizing = window.ProcessResize(this, resizeId);

        // Process title bar drag (strictly blocked while resizing)
        float titleBarHeight = LineHeight;
        var titleBarSize     = new Vector2(window.size.X, titleBarHeight);
        int titleBarId       = WidgetID.CombineHash(parentHash, "__titlebar".GetHashCode());

        bool isTitleHover = !isResizing && window.IsHoverAt(window.pos, titleBarSize, draw);
        var titleState    = GetWidgetState(isTitleHover, titleBarId);

        if (titleState == WidgetState.Down) {
            window.pos += input.MouseDelta;
        }

        // Render background & titlebar
        draw.FillRectRounded(window.pos, window.size, 8, Color.WindowColor);

        var headerColor = Color.ButtonState(titleState);
        draw.FillRectRounded(window.pos, titleBarSize, 8, headerColor);

        var fontHeight = LineHeight;
        var textPos    = window.pos + new Vector2(10f, (titleBarHeight - fontHeight) / 2f);
        draw.DrawText(title, textPos, Color.TextColor);

        window.SetCursor(window.pos + new Vector2(10f, titleBarHeight + 10f));
        
        // --- Push content scissor rect (clips everything below titlebar) ---
        var contentPos  = window.pos + new Vector2(0f, titleBarHeight);
        var contentSize = new Vector2(window.size.X, Math.Max(0f, window.size.Y - titleBarHeight));
        draw.PushScissor(contentPos, contentSize);
        return new WindowScope(this, true);
    }
    
    internal void EndWindow()
    {
        draw.PopScissor();
        draw.PopZIndex();
        Window.ClearScope();
    }
    
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
        /* if (window.IsHoverAt(parentStartCursor, finalSize, draw)) {
            float wheel = input.MouseWheelDelta;
            if (wheel != 0f) {
                scrollState.offsetY -= wheel * 20f; // 20px per wheel notch
                scrollState.offsetY = Math.Max(0f, scrollState.offsetY); // Prevent negative offset
            }
        } */
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
        float maxScroll = Math.Max(0f, contentSize.Y - childSize.Y);
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
        float visibleRatio = size.Y / totalContentHeight;
        float thumbHeight = Math.Max(20f, size.Y * visibleRatio);
        float scrollableRange = totalContentHeight - size.Y;
        float thumbScrollableRange = size.Y - thumbHeight;

        float thumbY = (scrollState.offsetY / scrollableRange) * thumbScrollableRange;
        Vector2 thumbPos = new Vector2(trackPos.X, trackPos.Y + thumbY);
        Vector2 thumbSize = new Vector2(trackWidth, thumbHeight);

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


#region Widgets

    internal void Label(ReadOnlySpan<char> name, Color32 textColor)
    {
        var window = Window;
        if (textColor.Packed == 0) textColor = Color.TextColor;
        
        var size = draw.DrawText(name, window.Cursor, textColor);
        
        window.MoveCursor(size);
    }
    
    internal void Spacer(float size)
    {
        var window      = Window;
        var spaceSize   = window.CurrentLayout.direction == LayoutDirection.Horizontal ? new Vector2(size, 0) : new Vector2(0, size);
        window.MoveCursor(spaceSize);
    }
    
    internal bool Button(ReadOnlySpan<char> name, GuiStyle? style, WidgetID id)
    {
        var window = Window;
        using var __ = UseStyle(style);
        
        int parentHash  = window.GetCurrentScopeHash();
        int widgetId    = id.Resolve(name, parentHash);
        
        var size    = draw.MeasureText(name);
        var isHover = window.IsHoverAtCursor(size, draw);

        // Calculate widget center & register for 1D/2D navigation
        var center = window.Cursor + size * 0.5f;
        bool isFocused = RegisterFocusable(widgetId, center, out _);

        var widgetState = GetWidgetState(isHover, widgetId);
        
        draw.FillRectRounded(window.Cursor, size, 8, Color.ButtonState(widgetState)); // background

        if (isFocused) {
            draw.StrokeRect(window.Cursor, size, 4, Color.FocusColor);
        }
        draw.DrawTextInRect(name, window.Cursor, size, TextAlignment.Center, VerticalAlignment.Middle, Color.ButtonText);
        window.MoveCursor(size);
        return IsFired(widgetState, isFocused);
    }
    
    internal bool Checkbox(ReadOnlySpan<char> name, ref bool value, GuiStyle? style, WidgetID id)
    {
        var window = Window;
        using var __ = UseStyle(style);
        int parentHash = window.GetCurrentScopeHash();
        int widgetId   = id.Resolve(name, parentHash);

        var boxSize   = LineHeight;
        var textSize  = draw.MeasureText(name);
        var totalSize = new Vector2(boxSize + 8f + textSize.X, Math.Max(boxSize, textSize.Y));

        var isHover = window.IsHoverAtCursor(totalSize, draw);

        // Register focus for 1D/2D navigation
        var center      = window.Cursor + totalSize * 0.5f;
        bool isFocused  = RegisterFocusable(widgetId, center, out _);

        var widgetState = GetWidgetState(isHover, widgetId);

        bool isToggled = IsFired(widgetState, isFocused);
        if (isToggled) {
            value = !value;
        }
        var boxRect = new Vector2(window.Cursor.X, window.Cursor.Y + (totalSize.Y - boxSize) / 2f);
        draw.FillRectRounded(boxRect, new Vector2(boxSize, boxSize), 4, Color.ButtonState(widgetState)); // background

        // Render blue focus outline on box
        if (isFocused) {
            draw.StrokeRect(boxRect, new Vector2(boxSize, boxSize), 4, Color.FocusColor);
        }
        if (value) {
            var padding = boxSize / 6;
            var innerRect = new Vector2(boxRect.X + padding, boxRect.Y + padding);
            draw.FillRectRounded(innerRect, new Vector2(boxSize - 2 * padding, boxSize - 2 * padding), 8, Color.TextColor);
        }
        var textPos = new Vector2(boxRect.X + boxSize + 8f, window.Cursor.Y + (totalSize.Y - textSize.Y) / 2f);
        draw.DrawText(name, textPos, Color.TextColor);

        window.MoveCursor(totalSize);
        return isToggled;
    }
    
    internal bool Slider(ReadOnlySpan<char> name, ref float value, float min, float max, float width, ReadOnlySpan<char> format, GuiStyle? style, WidgetID id)
    {
        var window      = Window;
        using var __    = UseStyle(style);
        int parentHash  = window.GetCurrentScopeHash();
        int widgetId    = id.Resolve(name, parentHash);

        float height    = LineHeight;
        var totalSize   = new Vector2(width, height);

        var isHover     = window.IsHoverAtCursor(totalSize, draw);

        // Register focus for 1D/2D navigation
        var center      = window.Cursor + totalSize * 0.5f;
        bool isFocused  = RegisterFocusable(widgetId, center, out _);

        var widgetState = GetWidgetState(isHover, widgetId);

        bool changed = false;

        if (widgetState == WidgetState.Down) {
            float t = Math.Clamp((input.MousePos.X - window.Cursor.X) / width, 0f, 1f);
            float newValue = min + t * (max - min);
            
            if (newValue != value) {
                value = newValue;
                changed = true;
            }
        }
        draw.FillRectRounded(window.Cursor, totalSize, 6, Color.ButtonState(widgetState)); // background

        // Fill bar
        float tVal = Math.Clamp((value - min) / (max - min), 0f, 1f);
        var fillSize = new Vector2(width * tVal, height);
        
        draw.FillRectRounded(window.Cursor, fillSize, 6, Color.SliderFill);

        // Render blue focus outline
        if (isFocused) {
            draw.StrokeRect(window.Cursor, totalSize, 4, Color.FocusColor);
        }
        var labelText = StringBuilder().AppendFloat(value, format.IsEmpty ? "F1" : format, FormatProvider);
        draw.DrawTextInRect(labelText.Span(), window.Cursor, totalSize, TextAlignment.Center, VerticalAlignment.Middle, Color.TextColor);

        window.MoveCursor(totalSize);
        return changed;
    }
    
   
    internal SpaceScope BeginSpace(Vector2 size, WidgetID id)
    {
        var window      = Window;
        var pos         = window.Cursor;
        var widgetState = WidgetState.None;
        var isFocused   = false;

        if (id.IsValid) {
            int parentHash = window.GetCurrentScopeHash();
            int widgetId   = id.Resolve(parentHash);
            bool isHover   = window.IsHoverAt(pos, size, draw);
            
            widgetState = GetWidgetState(isHover, widgetId);

            var center = pos + size * 0.5f;
            isFocused  = RegisterFocusable(widgetId, center, out _);
        }
        window.MoveCursor(size);

        bool isFired = IsFired(widgetState, isFocused);
        return new SpaceScope(this, pos, size, isFired, isFocused, widgetState);
    }

    internal void EndSpace(SpaceScope space)
    {
        if (!space.isFocused) return;
        draw.StrokeRect(space.pos, space.size, 4, Color.FocusColor);
    }

#endregion

    
#region Layout
    internal VerticalScope BeginVertical()
    {
        Window.PushLayout(LayoutDirection.Vertical);
        return new VerticalScope(this);
    }

    internal void EndVertical() => Window.PopLayout();
    
    internal HorizontalScope BeginHorizontal()
    {
        Window.PushLayout(LayoutDirection.Horizontal);
        return new HorizontalScope(this);
    }
    internal void EndHorizontal() => Window.PopLayout();
#endregion


#region Styles
    internal StyleScope PushStyle(GuiStyle style)
    {
        var revertStyles = guiState.revertStyles;
        var length       = revertStyles.Length;
        if (guiState.revertStylesCount >= length) {
            revertStyles = new RevertStyle[Math.Max(4, 2 * length)]; 
            Array.Copy(guiState.revertStyles,  revertStyles, length);
            guiState.revertStyles = revertStyles; 
        }
        ref var revertStyle = ref revertStyles[guiState.revertStylesCount++];
        guiState.currentStyle.PushOverrides(style, ref revertStyle);
        return new StyleScope(this);
    }
    
    internal void PopStyle()
    {
        ref var revertStyle = ref guiState.revertStyles[--guiState.revertStylesCount];
        guiState.currentStyle.PopOverrides(revertStyle);
    }
#endregion
}

