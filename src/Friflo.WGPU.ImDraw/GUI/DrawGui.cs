// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Diagnostics;
using System.Numerics;
using System.Text;

// ReSharper disable ConvertIfStatementToReturnStatement
// ReSharper disable InconsistentNaming
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UseWithExpressionToCopyStruct
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;


public readonly ref struct DrawGui : IDisposable
{
    public readonly GuiWidget   widget;
    public          Draw2D      draw => widget.draw;
    
    internal DrawGui(Draw2D draw, Batch2D batch) {
        widget = new GuiWidget(draw, batch);
    }
    
    public void Dispose() {
        widget.draw.Dispose();
    }
    
    public void SetNextWindowPos(Vector2 position)  => widget.SetNextWindowPos(position);
    public void SetNextWindowSize(Vector2 size)     => widget.SetNextWindowSize(size);
    
    public WindowScope  BeginWindow(string title)   => widget.BeginWindow(title);
    public void         EndWindow()                 => widget.EndWindow();
    
    public void Label(ReadOnlySpan<char> name, Color32 textColor = default)
        => widget.Label(name, textColor);
    
    public bool Button(ReadOnlySpan<char> name, GuiStyle? style = null, WidgetID id = default)
        => widget.Button(name, style, id);
    
    public bool Checkbox(ReadOnlySpan<char> name, ref bool value, GuiStyle? style = null, WidgetID id = default)
        => widget.Checkbox(name, ref value, style, id);
    
    public bool Slider(float width, ReadOnlySpan<char> name, ref float value, ReadOnlySpan<char> format, float min, float max, GuiStyle? style = null, WidgetID id = default)
        => widget.Slider(width, name, ref value, format, min, max, style, id);
    
    public bool ReserveSpace(out Vector2 pos, Vector2 size, out bool isFocused, out WidgetState widgetState, WidgetID id = default)
        => widget.ReserveSpace(out pos, size, out isFocused, out widgetState, id);
    
    public void DrawFocusRect(Vector2 pos, Vector2 size, bool isFocused, float margin = 4f)
        => widget.DrawFocusRect(pos, size, isFocused, margin);
    
    public StyleScope       PushStyle(GuiStyle style)   => widget.PushStyle(style);
    public void             PopStyle()                  => widget.PopStyle();
    
    public VerticalScope    BeginVertical()             => widget.BeginVertical();
    public void             EndVertical()               => widget.EndVertical();
    
    public HorizontalScope  BeginHorizontal()           => widget.BeginHorizontal();
    public void             EndHorizontal()             => widget.EndHorizontal();
}
    

public readonly ref struct GuiWidget
{
    public  readonly    Draw2D          draw;           // 16 bytes
    public  readonly    GuiInput        input;          //  8 bytes
    private readonly    GuiState        guiState;       //  8 bytes
    private readonly    GuiStyle        currentStyle;   //  8 bytes
    
    public ref readonly GuiColor        Color           { [DebuggerStepThrough] get => ref currentStyle.color; }
    public              GuiWindow       Window          { [DebuggerStepThrough] get => guiState.window; }
    public              float           LineHeight      { [DebuggerStepThrough] get => draw.DefaultFont.lineHeight; }
    public              IFormatProvider FormatProvider  { [DebuggerStepThrough] get => draw.batch.formatProvider; }

    
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
    
    internal GuiWidget(Draw2D draw, Batch2D batch) {
        this.draw       = draw;
        input           = batch.input;
        guiState        = batch.guiState;
        guiState.SetFrameCount(input.FrameCount);
        currentStyle    = guiState.currentStyle;
    }
#region Window


    public void SetNextWindowPos(Vector2 position)
    {
        guiState.nextWindowPos = position;
    }

    public void SetNextWindowSize(Vector2 size)
    {
        guiState.nextWindowSize = size;
    }
    
    public WindowScope BeginWindow(string title)
    {
        var gui = draw.batch.gui;
        if (!gui.windows.TryGetValue(title, out guiState.window!)) {
            guiState.window = new GuiWindow(gui, title) {
                pos     = guiState.nextWindowPos  ?? new Vector2(50, 50),
                size    = guiState.nextWindowSize ?? new Vector2(300, 200)
            };
            gui.windows.Add(title, guiState.window);
            gui.windowOrder.Add(guiState.window);
        }
        guiState.nextWindowPos  = null;
        guiState.nextWindowSize = null;
        var window      = Window;
        
        // Hit test whole window
        bool isWindowHovered = window.IsHoverAt(window.pos, window.size, draw);

        // Focus window on click (WITHOUT capturing activeItem)
        if (isWindowHovered && input.IsMouseDown) {
            // Note: Moving window to front here ensures that subsequent child widgets 
            //       in this same frame pass the IsTopWindowAt() check and process clicks immediately.
            gui.FocusWindow(window);
        }
        draw.PushZIndex(gui.windowOrder.IndexOf(window) + 1); // +1, so 0 is background;
        
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

        var headerColor = titleState switch {
            WidgetState.Hover   => Color.ButtonHover,
            WidgetState.Down    => Color.ButtonDown,
            _                   => Color.ButtonColor
        };
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
    
    public void EndWindow()
    {
        draw.PopScissor();
        draw.PopZIndex();
        Window.ClearScope();
    }
#endregion


#region Widgets

    public void Label(ReadOnlySpan<char> name, Color32 textColor)
    {
        var window = Window;
        if (textColor.Packed == 0) textColor = Color.TextColor;
        
        var size = draw.DrawText(name, window.Cursor, textColor);
        
        window.MoveCursor(size);
    }
    
    public bool Button(ReadOnlySpan<char> name, GuiStyle? style, WidgetID id)
    {
        var window = Window;
        if (style != null) PushStyle(style);
        
        int parentHash  = window.GetCurrentScopeHash();
        int widgetId    = id.Resolve(name, parentHash);
        
        var size    = draw.MeasureText(name);
        var isHover = window.IsHoverAtCursor(size, draw);

        // Calculate widget center & register for 1D/2D navigation
        var center = window.Cursor + size * 0.5f;
        bool isFocused = RegisterFocusable(widgetId, center, out _);

        var widgetState = GetWidgetState(isHover, widgetId);
        
        var buttonColor = widgetState switch {
            WidgetState.Down    => Color.ButtonDown,
            WidgetState.Hover   => Color.ButtonHover,
            _                   => Color.ButtonColor
        };
        // Render button background
        draw.FillRectRounded(window.Cursor, size, 8, buttonColor);

        if (isFocused) {
            draw.StrokeRect(window.Cursor, size, 4, Color.FocusColor);
        }
        draw.DrawTextInRect(name, window.Cursor, size, TextAlignment.Center, VerticalAlignment.Middle, Color.ButtonText);
        
        window.MoveCursor(size);
        
        if (style != null) PopStyle();
        // Trigger click via mouse or keyboard (Enter/Space when focused)
        bool isKeySubmitted = isFocused && input.IsSubmitPressed;
        return widgetState == WidgetState.Clicked || isKeySubmitted;
    }
    
    public bool Checkbox(ReadOnlySpan<char> name, ref bool value, GuiStyle? style, WidgetID id)
    {
        var window = Window;
        if (style != null) PushStyle(style);
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

        // Toggle value via mouse click or keyboard submit (Enter/Space)
        bool clicked = widgetState == WidgetState.Clicked || (isFocused && input.IsSubmitPressed);
        if (clicked) {
            value = !value;
        }
        var boxColor = widgetState switch {
            WidgetState.Down    => Color.ButtonDown,
            WidgetState.Hover   => Color.ButtonHover,
            _                   => Color.ButtonColor
        };
        var boxRect = new Vector2(window.Cursor.X, window.Cursor.Y + (totalSize.Y - boxSize) / 2f);
        draw.FillRectRounded(boxRect, new Vector2(boxSize, boxSize), 4, boxColor);

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
        if (style != null) PopStyle();
        return clicked;
    }
    
    public bool Slider(float width, ReadOnlySpan<char> name, ref float value, ReadOnlySpan<char> format, float min, float max, GuiStyle? style, WidgetID id)
    {
        var window      = Window;
        if (style != null) PushStyle(style);
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
            float t = Math.Clamp((input.Mouse.X - window.Cursor.X) / width, 0f, 1f);
            float newValue = min + t * (max - min);
            
            if (newValue != value) {
                value = newValue;
                changed = true;
            }
        }
        var slideBg = widgetState switch {
            WidgetState.Down    => Color.ButtonDown,
            WidgetState.Hover   => Color.ButtonHover,
            _                   => Color.SliderColor
        };
        draw.FillRectRounded(window.Cursor, totalSize, 6, slideBg);

        // Fill bar
        float tVal = Math.Clamp((value - min) / (max - min), 0f, 1f);
        var fillSize = new Vector2(width * tVal, height);
        
        draw.FillRectRounded(window.Cursor, fillSize, 6, Color.SliderFill);

        // Render blue focus outline
        if (isFocused) {
            draw.StrokeRect(window.Cursor, totalSize, 4, Color.FocusColor);
        }
        var labelText = StringBuilder().AppendFloat(value, format, FormatProvider);
        draw.DrawTextInRect(labelText.Span(), window.Cursor, totalSize, TextAlignment.Center, VerticalAlignment.Middle, Color.TextColor);

        window.MoveCursor(totalSize);
        if (style != null) PopStyle();
        return changed;
    }
    
   
    public bool ReserveSpace(
        out Vector2     pos,
            Vector2     size,
        out bool        isFocused,
        out WidgetState widgetState,
            WidgetID    id)
    {
        var window  = Window;
        pos         = window.Cursor;
        widgetState = WidgetState.None;
        isFocused   = false;

        if (id.IsValid)
        {
            int parentHash = window.GetCurrentScopeHash();
            int widgetId   = id.Resolve(parentHash);
            bool isHover   = window.IsHoverAt(pos, size, draw);
            
            widgetState = GetWidgetState(isHover, widgetId);

            var center = pos + size * 0.5f;
            isFocused  = RegisterFocusable(widgetId, center, out _);
        }
        window.MoveCursor(size);

        bool isKeySubmitted = isFocused && input.IsSubmitPressed;
        return widgetState == WidgetState.Clicked || isKeySubmitted;
    }

    public void DrawFocusRect(Vector2 pos, Vector2 size, bool isFocused, float margin)
    {
        if (!isFocused) return;
        var offset = new Vector2(margin, margin);
        draw.StrokeRect(pos - offset, size + 2f * offset, 4, Color.FocusColor);
    }

#endregion

    
#region Layout
    public VerticalScope BeginVertical()
    {
        Window.PushLayout(LayoutDirection.Vertical);
        return new VerticalScope(this);
    }

    public void EndVertical() => Window.PopLayout();
    
    public HorizontalScope BeginHorizontal()
    {
        Window.PushLayout(LayoutDirection.Horizontal);
        return new HorizontalScope(this);
    }
    public void EndHorizontal() => Window.PopLayout();
#endregion


#region Styles
    public StyleScope PushStyle(GuiStyle style)
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
    
    public void PopStyle()
    {
        ref var revertStyle = ref guiState.revertStyles[--guiState.revertStylesCount];
        guiState.currentStyle.PopOverrides(revertStyle);
    }
#endregion
}

