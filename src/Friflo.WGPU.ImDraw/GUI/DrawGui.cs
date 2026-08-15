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
    private readonly    Batch2D     batch;
    private             Draw2D      draw;
    private             Window      window;
    
    internal DrawGui(Draw2D draw, Batch2D batch) {
        this.draw   = draw;
        this.batch  = batch;
        window      = null!;
    }
    
    public void Dispose()
    {
        draw.Dispose();
    }

#region Window
    private Vector2? nextWindowPos;
    private Vector2? nextWindowSize;

    public void SetNextWindowPos(Vector2 position)
    {
        nextWindowPos = position;
    }

    public void SetNextWindowSize(Vector2 size)
    {
        nextWindowSize = size;
    }
    
    public void BeginWindow(string title, Color32 color = default)
    {
        if (!batch.windows.TryGetValue(title, out window!)) {
            window = new Window {
                position = nextWindowPos  ?? new Vector2(50, 50),
                size     = nextWindowSize ?? new Vector2(300, 200)
            };
            batch.windows.Add(title, window);
        }
        nextWindowPos  = null;
        nextWindowSize = null;

        window.ResetScope(title);

        float titleBarHeight = batch.GetDefaultFont().lineHeight;
        var titleBarSize = new Vector2(window.size.X, titleBarHeight);

        int parentHash  = window.GetCurrentScopeHash();
        int titleBarId  = WidgetID.CombineHash(parentHash, "__titlebar".GetHashCode());

        bool isTitleHover = Window.IsHoverAt(batch, window.position, titleBarSize);
        var titleState    = batch.input.GetWidgetState(isTitleHover, titleBarId);

        if (titleState == WidgetState.Down) {
            window.position += batch.input.MouseDelta;
        }

        if (color.Packed == 0) color = 0x222222ff;

        draw.RectangleRounded(window.position, window.size, 8, color);

        var headerColor = window.buttonColor;
        if (titleState == WidgetState.Hover) headerColor = window.buttonHover;
        if (titleState == WidgetState.Down)  headerColor = window.buttonDown;

        draw.RectangleRounded(window.position, titleBarSize, 8, headerColor);

        var fontHeight = draw.GetDefaultFont().lineHeight;
        var textPos    = window.position + new Vector2(10f, (titleBarHeight - fontHeight) / 2f);
        draw.DrawString(title, textPos, window.textColor);

        window.cursor = window.position + new Vector2(10f, titleBarHeight + 10f);
    }
    
    public void EndWindow()
    {
        window.ClearScope();
    }
#endregion


#region Widgets

    public void Label(ReadOnlySpan<char> name, Color32 textColor = default)
    {
        if (textColor.Packed == 0) textColor = window.textColor;
        
        var size = draw.DrawString(name, window.cursor, textColor);
        
        window.MoveCursor(size);
    }
    
    public bool Button(ReadOnlySpan<char> name, WidgetID id = default, Color32 color = default, Color32 textColor = default)
    {
        if (color.Packed == 0)      color       = window.buttonColor;
        if (textColor.Packed == 0)  textColor   = window.textColor;
        
        int parentHash  = window.GetCurrentScopeHash();
        int widgetId    = id.Resolve(name, parentHash);
        
        var size    = draw.MeasureString(name);
        var isHover = window.IsHover(batch, size);

        var widgetState = batch.input.GetWidgetState(isHover, widgetId);
        
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
    
    public bool Checkbox(ReadOnlySpan<char> name, ref bool value, WidgetID id = default)
    {
        int parentHash = window.GetCurrentScopeHash();
        int widgetId   = id.Resolve(name, parentHash);

        var boxSize   = draw.GetDefaultFont().lineHeight;
        var textSize  = draw.MeasureString(name);
        var totalSize = new Vector2(boxSize + 8f + textSize.X, Math.Max(boxSize, textSize.Y));

        var isHover = window.IsHover(batch, totalSize);
        var widgetState = batch.input.GetWidgetState(isHover, widgetId);

        if (widgetState == WidgetState.Clicked) {
            value = !value;
        }

        Color32 boxColor = window.buttonColor;
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
    
    public bool Slider(float width, ReadOnlySpan<char> name, ref float value, ReadOnlySpan<char> format, float min, float max, WidgetID id = default)
    {
        int parentHash = window.GetCurrentScopeHash();
        int widgetId   = id.Resolve(name, parentHash);

        float height    = draw.GetDefaultFont().lineHeight;
        var totalSize   = new Vector2(width, height);

        var isHover     = window.IsHover(batch, totalSize);
        var widgetState = batch.input.GetWidgetState(isHover, widgetId);

        bool changed = false;

        if (widgetState == WidgetState.Down) {
            float t = Math.Clamp((batch.input.Mouse.X - window.cursor.X) / width, 0f, 1f);
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
        
        Color32 barColor = window.buttonHover;
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
    public void BeginVertical()     => window.PushLayout(LayoutDirection.Vertical);
    public void EndVertical()       => window.PopLayout();
    
    public void BeginHorizontal()   => window.PushLayout(LayoutDirection.Horizontal);
    public void EndHorizontal()     => window.PopLayout();
#endregion
}