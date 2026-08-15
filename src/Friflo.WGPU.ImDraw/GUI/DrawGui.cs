// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Numerics;

// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ConvertSwitchStatementToSwitchExpression
// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;


public ref struct DrawGui : IDisposable
{
    private readonly    GuiInput    input;
    private readonly    Gui         gui;
    public              Draw2D      draw;
    
    private readonly    GuiWindow   Window      => gui.window;
    private readonly    float       LineHeight  => draw.GetDefaultFont().lineHeight;
    
    internal DrawGui(Draw2D draw, Batch2D batch) {
        this.draw   = draw;
        input       = batch.input;
        gui         = batch.gui;
    }
    
    public void Dispose()
    {
        draw.Dispose();
    }

#region Window


    public readonly void SetNextWindowPos(Vector2 position)
    {
        gui.nextWindowPos = position;
    }

    public readonly void SetNextWindowSize(Vector2 size)
    {
        gui.nextWindowSize = size;
    }
    
    public readonly void BeginWindow(string title, Color32 color = default)
    {
        if (!gui.windows.TryGetValue(title, out gui.window!)) {
            gui.window = new GuiWindow {
                position = gui.nextWindowPos  ?? new Vector2(50, 50),
                size     = gui.nextWindowSize ?? new Vector2(300, 200)
            };
            gui.windows.Add(title, gui.window);
            gui.windowOrder.Add(gui.window);
        }
        gui.nextWindowPos  = null;
        gui.nextWindowSize = null;
        var window      = Window;
        
        // hit test whole window
        var windowSize = window.size;
        bool isWindowHovered = window.IsHoverAt(gui, input, window.position, windowSize);

        // if clicked -> Focus / update Z-Order
        if (isWindowHovered && input.GetWidgetState(true, window.GetCurrentScopeHash()) == WidgetState.Down) {
            // Note: Moving window to front here ensures that subsequent child widgets 
            //       in this same frame pass the IsTopWindowAt() check and process clicks immediately.
            gui.FocusWindow(window);
        }
        draw.SetZIndex(gui.windowOrder.IndexOf(window) + 1);  // +1, so 0 is background;
        
        window.ResetScope(title);

        float titleBarHeight = LineHeight;
        var titleBarSize = new Vector2(window.size.X, titleBarHeight);

        int parentHash  = window.GetCurrentScopeHash();
        int titleBarId  = WidgetID.CombineHash(parentHash, "__titlebar".GetHashCode());

        bool isTitleHover = window.IsHoverAt(gui, input, window.position, titleBarSize);
        var titleState    = input.GetWidgetState(isTitleHover, titleBarId);

        if (titleState == WidgetState.Down) {
            window.position += input.MouseDelta;
        }

        if (color.Packed == 0) color = 0x222222ff;

        draw.RectangleRounded(window.position, window.size, 8, color);

        var headerColor = window.buttonColor;
        if (titleState == WidgetState.Hover) headerColor = window.buttonHover;
        if (titleState == WidgetState.Down)  headerColor = window.buttonDown;

        draw.RectangleRounded(window.position, titleBarSize, 8, headerColor);

        var fontHeight = LineHeight;
        var textPos    = window.position + new Vector2(10f, (titleBarHeight - fontHeight) / 2f);
        draw.DrawString(title, textPos, window.textColor);

        window.cursor = window.position + new Vector2(10f, titleBarHeight + 10f);
    }
    
    public readonly void EndWindow()
    {
        draw.SetZIndex(0);
        Window.ClearScope();
    }
#endregion


#region Widgets

    public readonly void Label(ReadOnlySpan<char> name, Color32 textColor = default)
    {
        var window = Window;
        if (textColor.Packed == 0) textColor = window.textColor;
        
        var size = draw.DrawString(name, window.cursor, textColor);
        
        window.MoveCursor(size);
    }
    
    public readonly bool Button(ReadOnlySpan<char> name, WidgetID id = default, Color32 color = default, Color32 textColor = default)
    {
        var window = Window;
        if (color.Packed == 0)      color       = window.buttonColor;
        if (textColor.Packed == 0)  textColor   = window.textColor;
        
        int parentHash  = window.GetCurrentScopeHash();
        int widgetId    = id.Resolve(name, parentHash);
        
        var size    = draw.MeasureString(name);
        var isHover = window.IsHover(gui, input, size);

        var widgetState = input.GetWidgetState(isHover, widgetId);
        
        switch (widgetState)
        {
            case WidgetState.Down:
                color = window.buttonDown;
                break;
            case WidgetState.Hover:
                color = window.buttonHover;
                break;
        }
        
        draw.RectangleRounded(window.cursor, size, 8, color);
        draw.DrawStringInRect(name, window.cursor, size, TextAlignment.Center, VerticalAlignment.Middle, textColor);
        
        window.MoveCursor(size);
        return widgetState == WidgetState.Clicked;
    }
    
    public readonly bool Checkbox(ReadOnlySpan<char> name, ref bool value, WidgetID id = default)
    {
        var window = Window;
        int parentHash = window.GetCurrentScopeHash();
        int widgetId   = id.Resolve(name, parentHash);

        var boxSize   = LineHeight;
        var textSize  = draw.MeasureString(name);
        var totalSize = new Vector2(boxSize + 8f + textSize.X, Math.Max(boxSize, textSize.Y));

        var isHover = window.IsHover(gui, input, totalSize);
        var widgetState = input.GetWidgetState(isHover, widgetId);

        if (widgetState == WidgetState.Clicked) {
            value = !value;
        }

        var boxColor = window.buttonColor;
        switch (widgetState)
        {
            case WidgetState.Down:
                boxColor = window.buttonDown;
                break;
            case WidgetState.Hover:
                boxColor = window.buttonHover;
                break;
        }

        var boxRect = new Vector2(window.cursor.X, window.cursor.Y + (totalSize.Y - boxSize) / 2f);
        draw.RectangleRounded(boxRect, new Vector2(boxSize, boxSize), 4, boxColor);

        if (value) {
            var padding = boxSize / 6;
            var innerRect = new Vector2(boxRect.X + padding, boxRect.Y + padding);
            draw.RectangleRounded(innerRect, new Vector2(boxSize - 2 * padding, boxSize - 2 * padding), 8, window.textColor);
        }

        var textPos = new Vector2(boxRect.X + boxSize + 8f, window.cursor.Y + (totalSize.Y - textSize.Y) / 2f);
        draw.DrawString(name, textPos, window.textColor);

        window.MoveCursor(totalSize);

        return widgetState == WidgetState.Clicked;
    }
    
    public readonly bool Slider(float width, ReadOnlySpan<char> name, ref float value, ReadOnlySpan<char> format, float min, float max, WidgetID id = default)
    {
        var window = Window;
        int parentHash = window.GetCurrentScopeHash();
        int widgetId   = id.Resolve(name, parentHash);

        float height    = LineHeight;
        var totalSize   = new Vector2(width, height);

        var isHover     = window.IsHover(gui, input, totalSize);
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

        draw.RectangleRounded(window.cursor, totalSize, 6, window.sliderColor);

        // fill bar
        float tVal = Math.Clamp((value - min) / (max - min), 0f, 1f);
        var fillSize = new Vector2(width * tVal, height);
        
        var barColor = window.buttonHover;
        if (widgetState == WidgetState.Down) {
            barColor = window.buttonDown;
        }
        
        draw.RectangleRounded(window.cursor, fillSize, 6, barColor);

        var labelText = window.Builder().AppendFormat(value, format);
        draw.DrawStringInRect(labelText.Span, window.cursor, totalSize, TextAlignment.Center, VerticalAlignment.Middle, window.textColor);

        window.MoveCursor(totalSize);
        return changed;
    }

#endregion

    
#region Layout
    public readonly void BeginVertical()     => Window.PushLayout(LayoutDirection.Vertical);
    public readonly void EndVertical()       => Window.PopLayout();
    
    public readonly void BeginHorizontal()   => Window.PushLayout(LayoutDirection.Horizontal);
    public readonly void EndHorizontal()     => Window.PopLayout();
#endregion
}