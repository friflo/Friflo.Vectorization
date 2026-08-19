// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Diagnostics;
using System.Numerics;
using System.Text;

// ReSharper disable InconsistentNaming
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UseWithExpressionToCopyStruct
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;


public readonly ref struct DrawGui : IDisposable
{
    public  readonly    Draw2D      draw;           // 16 bytes
    private readonly    GuiInput    input;          //  8 bytes
    private readonly    GuiState    guiState;       //  8 bytes
    private readonly    GuiStyle    currentStyle;   //  8 bytes
    
    public ref readonly GuiColor    Color       { [DebuggerStepThrough] get => ref currentStyle.color; }
    private             Gui         Gui         { [DebuggerStepThrough] get => draw.batch.gui; }
    private             GuiWindow   Window      { [DebuggerStepThrough] get => guiState.window; }
    private             float       LineHeight  { [DebuggerStepThrough] get => draw.DefaultFont.lineHeight; }
    
    /// <summary> Clears and returns a cached <see cref="System.Text.StringBuilder"/> to prevent allocations. </summary>
    private             StringBuilder   StringBuilder() => draw.batch.StringBuilder();

    
    internal DrawGui(Draw2D draw, Batch2D batch) {
        this.draw       = draw;
        input           = batch.input;
        guiState        = batch.guiState;
        currentStyle    = guiState.currentStyle;
    }
    
    public void Dispose()
    {
        draw.Dispose();
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
        var gui = Gui;
        if (!gui.windows.TryGetValue(title, out guiState.window!)) {
            guiState.window = new GuiWindow(gui) {
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
        
        window.ResetScope(title);
        int parentHash = window.GetCurrentScopeHash();

        // Process window resize
        int resizeId 	= WidgetID.CombineHash(parentHash, "__resize".GetHashCode());
        bool isResizing = window.ProcessResize(input, resizeId);

        // Process title bar drag (strictly blocked while resizing)
        float titleBarHeight = LineHeight;
        var titleBarSize     = new Vector2(window.size.X, titleBarHeight);
        int titleBarId       = WidgetID.CombineHash(parentHash, "__titlebar".GetHashCode());

        bool isTitleHover = !isResizing && window.IsHoverAt(window.pos, titleBarSize, draw);
        var titleState    = input.GetWidgetState(isTitleHover, titleBarId);

        if (titleState == WidgetState.Down) {
            window.pos += input.MouseDelta;
        }

        // Render background & titlebar
        draw.RectangleRounded(window.pos, window.size, 8, Color.WindowColor);

        var headerColor = titleState switch {
            WidgetState.Hover   => Color.ButtonHover,
            WidgetState.Down    => Color.ButtonDown,
            _                   => Color.ButtonColor
        };
        draw.RectangleRounded(window.pos, titleBarSize, 8, headerColor);

        var fontHeight = LineHeight;
        var textPos    = window.pos + new Vector2(10f, (titleBarHeight - fontHeight) / 2f);
        draw.DrawString(title, textPos, Color.TextColor);

        window.cursor = window.pos + new Vector2(10f, titleBarHeight + 10f);
        
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

    public void Label(ReadOnlySpan<char> name, Color32 textColor = default)
    {
        var window = Window;
        if (textColor.Packed == 0) textColor = Color.TextColor;
        
        var size = draw.DrawString(name, window.cursor, textColor);
        
        window.MoveCursor(size);
    }
    
    public bool Button(ReadOnlySpan<char> name, GuiStyle? style = null, WidgetID id = default)
    {
        var window = Window;
        if (style != null) PushStyle(style);
        
        int parentHash  = window.GetCurrentScopeHash();
        int widgetId    = id.Resolve(name, parentHash);
        
        var size    = draw.MeasureString(name);
        var isHover = window.IsHover(size, draw);

        // Calculate widget center & register for 1D/2D navigation
        var center = window.cursor + size * 0.5f;
        bool isFocused = input.RegisterFocusable(widgetId, center, out _);

        var widgetState = input.GetWidgetState(isHover, widgetId);
        
        var buttonColor = widgetState switch {
            WidgetState.Down    => Color.ButtonDown,
            WidgetState.Hover   => Color.ButtonHover,
            _                   => Color.ButtonColor
        };
        // Render button background
        draw.RectangleRounded(window.cursor, size, 8, buttonColor);

        if (isFocused) {
            var focusColor = Color.FocusColor;
            draw.RectangleLines(window.cursor, size, 4, focusColor);
        }

        draw.DrawStringInRect(name, window.cursor, size, TextAlignment.Center, VerticalAlignment.Middle, Color.ButtonText);
        
        window.MoveCursor(size);
        
        if (style != null) PopStyle();
        // Trigger click via mouse or keyboard (Enter/Space when focused)
        bool isKeySubmitted = isFocused && input.IsSubmitPressed;
        return widgetState == WidgetState.Clicked || isKeySubmitted;
    }
    
    public bool Checkbox(ReadOnlySpan<char> name, ref bool value, GuiStyle? style = null, WidgetID id = default)
    {
        var window = Window;
        if (style != null) PushStyle(style);
        int parentHash = window.GetCurrentScopeHash();
        int widgetId   = id.Resolve(name, parentHash);

        var boxSize   = LineHeight;
        var textSize  = draw.MeasureString(name);
        var totalSize = new Vector2(boxSize + 8f + textSize.X, Math.Max(boxSize, textSize.Y));

        var isHover = window.IsHover(totalSize, draw);

        // Register focus for 1D/2D navigation
        var center      = window.cursor + totalSize * 0.5f;
        bool isFocused  = input.RegisterFocusable(widgetId, center, out _);

        var widgetState = input.GetWidgetState(isHover, widgetId);

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
        var boxRect = new Vector2(window.cursor.X, window.cursor.Y + (totalSize.Y - boxSize) / 2f);
        draw.RectangleRounded(boxRect, new Vector2(boxSize, boxSize), 4, boxColor);

        // Render blue focus outline on box
        if (isFocused) {
            var focusColor = Color.FocusColor;
            draw.RectangleLines(boxRect, new Vector2(boxSize, boxSize), 4, focusColor);
        }
        if (value) {
            var padding = boxSize / 6;
            var innerRect = new Vector2(boxRect.X + padding, boxRect.Y + padding);
            draw.RectangleRounded(innerRect, new Vector2(boxSize - 2 * padding, boxSize - 2 * padding), 8, Color.TextColor);
        }
        var textPos = new Vector2(boxRect.X + boxSize + 8f, window.cursor.Y + (totalSize.Y - textSize.Y) / 2f);
        draw.DrawString(name, textPos, Color.TextColor);

        window.MoveCursor(totalSize);
        if (style != null) PopStyle();
        return clicked;
    }
    
    public bool Slider(float width, ReadOnlySpan<char> name, ref float value, ReadOnlySpan<char> format, float min, float max, GuiStyle? style = null, WidgetID id = default)
    {
        var window      = Window;
        if (style != null) PushStyle(style);
        int parentHash  = window.GetCurrentScopeHash();
        int widgetId    = id.Resolve(name, parentHash);

        float height    = LineHeight;
        var totalSize   = new Vector2(width, height);

        var isHover     = window.IsHover(totalSize, draw);

        // Register focus for 1D/2D navigation
        var center      = window.cursor + totalSize * 0.5f;
        bool isFocused  = input.RegisterFocusable(widgetId, center, out _);

        var widgetState = input.GetWidgetState(isHover, widgetId);

        bool changed = false;

        if (widgetState == WidgetState.Down) {
            float t = Math.Clamp((input.Mouse.X - window.cursor.X) / width, 0f, 1f);
            float newValue = min + t * (max - min);
            
            if (newValue != value) {
                value = newValue;
                changed = true;
            }
        }
        draw.RectangleRounded(window.cursor, totalSize, 6, Color.SliderColor);

        // Fill bar
        float tVal = Math.Clamp((value - min) / (max - min), 0f, 1f);
        var fillSize = new Vector2(width * tVal, height);
        
        var barColor = widgetState switch {
            WidgetState.Down    => Color.ButtonDown,
            WidgetState.Hover   => Color.ButtonHover,
            _                   => Color.ButtonColor
        };
        draw.RectangleRounded(window.cursor, fillSize, 6, barColor);

        // Render blue focus outline
        if (isFocused) {
            var focusColor = Color.FocusColor;
            draw.RectangleLines(window.cursor, totalSize, 4, focusColor);
        }
        var labelText = StringBuilder().AppendFormat(value, format);
        draw.DrawStringInRect(labelText.Span, window.cursor, totalSize, TextAlignment.Center, VerticalAlignment.Middle, Color.TextColor);

        window.MoveCursor(totalSize);
        if (style != null) PopStyle();
        return changed;
    }
    
   
    public bool ReserveSpace(
        out Vector2     pos,
            Vector2     size,
        out bool        isFocused,
        out WidgetState widgetState,
            WidgetID    id          = default)
    {
        var window  = Window;
        pos         = window.cursor;
        widgetState = WidgetState.None;
        isFocused   = false;

        if (id.IsValid)
        {
            int parentHash = window.GetCurrentScopeHash();
            int widgetId   = id.Resolve(parentHash);
            bool isHover   = window.IsHoverAt(pos, size, draw);
            
            widgetState = input.GetWidgetState(isHover, widgetId);

            var center = pos + size * 0.5f;
            isFocused  = input.RegisterFocusable(widgetId, center, out _);
        }
        window.MoveCursor(size);

        bool isKeySubmitted = isFocused && input.IsSubmitPressed;
        return widgetState == WidgetState.Clicked || isKeySubmitted;
    }

    public void DrawFocusRect(Vector2 pos, Vector2 size, bool isFocused, float margin = 4f)
    {
        if (!isFocused) return;
        var focusColor  = Color.FocusColor;
        var offset      = new Vector2(margin, margin);
        draw.RectangleLines(pos - offset, size + 2f * offset, 4, focusColor);
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

