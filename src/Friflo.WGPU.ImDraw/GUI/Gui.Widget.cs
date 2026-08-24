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


public readonly ref partial struct GuiWidget
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

    public bool RegisterFocusable(int widgetId, Vector2 pos, Vector2 size, out bool gainedFocus)
    {
        if (guiState.IsNewFrame) {
            return input.RegisterFocusable(widgetId, pos, size, out gainedFocus);
        }
        gainedFocus = false;
        return false;
    }
    
    public WidgetState GetDragState(bool isHover, int widgetId)
    {
        if (guiState.IsNewFrame) {
            return input.GetDragState(isHover, widgetId);    
        }
        return WidgetState.None;
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
        return widgetState == WidgetState.Clicked || (isFocused && input.IsSubmitFired);
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
        bool isWindowHovered = !input.IsDragActive && window.IsHoverAt(window.pos, window.size, draw);

        // Focus window on click (WITHOUT capturing activeItem)
        if (isWindowHovered && input.IsMouseDown) {
            // Note: Moving window to front here ensures that subsequent child widgets 
            //       in this same frame pass the IsTopWindowAt() check and process clicks immediately.
            host.SetTopWindow(window);
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
        var titleState    = GetDragState(isTitleHover, titleBarId);

        if (titleState == WidgetState.Down) {
            window.pos += input.MousePosDelta;
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
        
        var size        = draw.MeasureText(name);
        var isHover     = window.IsHoverAtCursor(size, draw);
        bool isFocused  = RegisterFocusable(widgetId, window.Cursor, size, out _);
        var widgetState = GetWidgetState(isHover, widgetId);
        
        draw.FillRectRounded(window.Cursor, size, 8, Color.ButtonState(widgetState)); // background

        if (isFocused) {
            draw.StrokeRect(window.Cursor, size, 4, Color.FocusColor);
            window.EnsureVisibleInScrollArea(window.Cursor, size);
        }
        draw.DrawTextInRect(name, window.Cursor, size, TextAlignment.Center, VerticalAlignment.Middle, Color.ButtonText);
        window.MoveCursor(size);
        return IsFired(widgetState, isFocused);
    }
    
    internal bool Checkbox(ReadOnlySpan<char> name, ref bool value, GuiStyle? style, WidgetID id)
    {
        var window = Window;
        using var __ = UseStyle(style);
        int parentHash  = window.GetCurrentScopeHash();
        int widgetId    = id.Resolve(name, parentHash);

        var height      = LineHeight;
        var textSize    = draw.MeasureText(name);
        var totalSize   = new Vector2(height + 8f + textSize.X, Math.Max(height, textSize.Y));
        var isHover     = window.IsHoverAtCursor(totalSize, draw);
        bool isFocused  = RegisterFocusable(widgetId, window.Cursor, totalSize, out _);
        var widgetState = GetWidgetState(isHover, widgetId);
        bool isToggled  = IsFired(widgetState, isFocused);
        if (isToggled) {
            value = !value;
        }
        var boxRect = new Vector2(window.Cursor.X, window.Cursor.Y + (totalSize.Y - height) / 2f);
        draw.FillRectRounded(boxRect, new Vector2(height, height), 4, Color.ButtonState(widgetState)); // background

        // Render blue focus outline on box
        if (isFocused) {
            draw.StrokeRect(boxRect, new Vector2(height, height), 4, Color.FocusColor);
            window.EnsureVisibleInScrollArea(boxRect, new Vector2(height, height));
        }
        if (value) {
            var padding = height / 6;
            var innerRect = new Vector2(boxRect.X + padding, boxRect.Y + padding);
            draw.FillRectRounded(innerRect, new Vector2(height - 2 * padding, height - 2 * padding), 8, Color.TextColor);
        }
        var textPos = new Vector2(boxRect.X + height + 8f, window.Cursor.Y + (totalSize.Y - textSize.Y) / 2f);
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
        bool isFocused  = RegisterFocusable(widgetId, window.Cursor, totalSize, out _);
        var widgetState = GetWidgetState(isHover, widgetId);
        bool changed    = false;
        
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
            window.EnsureVisibleInScrollArea(window.Cursor, totalSize);
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
            int parentHash  = window.GetCurrentScopeHash();
            int widgetId    = id.Resolve(parentHash);
            
            bool isHover    = window.IsHoverAt(pos, size, draw);
            widgetState     = GetWidgetState(isHover, widgetId);
            isFocused       = RegisterFocusable(widgetId, pos, size, out _);
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

