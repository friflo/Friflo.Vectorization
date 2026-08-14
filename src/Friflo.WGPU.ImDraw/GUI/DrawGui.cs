// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Numerics;

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
    public void BeginWindow(string title, Vector2 position, Vector2 size, Color32 color)
    {
        if (!batch.windows.TryGetValue(title, out window!)) {
            window = new Window();
            batch.windows.Add(title, window);
        }
        window.ResetScope(title);
        
        draw.RectangleRounded(position, size, 8, color);
        window.cursor = position + new Vector2(10, 10);
    }
    
    public void EndWindow()
    {
        window.ClearScope();
    }
#endregion


#region Widgets

    public void Label(string name, Color32 textColor = default)
    {
        if (textColor.Packed == 0) textColor = window.textColor;
        
        var size = draw.DrawString(name, window.cursor, textColor);
        
        window.MoveCursor(size);
    }
    
    public bool Button(string name, WidgetID id = default, Color32 color = default, Color32 textColor = default)
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
    
    public bool Checkbox(string name, ref bool value, WidgetID id = default)
    {
        int parentHash = window.GetCurrentScopeHash();
        int widgetId   = id.Resolve(name, parentHash);

        var boxSize = 20f;
        var textSize = draw.MeasureString(name);
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
            var innerRect = new Vector2(boxRect.X + 5f, boxRect.Y + 5f);
            draw.RectangleRounded(innerRect, new Vector2(boxSize - 10f, boxSize - 10f), 2, window.textColor);
        }

        var textPos = new Vector2(boxRect.X + boxSize + 8f, window.cursor.Y + (totalSize.Y - textSize.Y) / 2f);
        draw.DrawString(name, textPos, window.textColor);

        window.MoveCursor(totalSize);

        return widgetState == WidgetState.Clicked;
    }

#endregion

    
#region Layout
    public void BeginVertical()     => window.PushLayout(LayoutDirection.Vertical);
    public void EndVertical()       => window.PopLayout();
    
    public void BeginHorizontal()   => window.PushLayout(LayoutDirection.Horizontal);
    public void EndHorizontal()     => window.PopLayout();
#endregion
}