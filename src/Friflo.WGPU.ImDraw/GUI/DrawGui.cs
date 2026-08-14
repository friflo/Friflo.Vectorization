// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Numerics;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;


public ref struct DrawGui
{
    private readonly    Batch2D     batch;
    private             Draw2D      draw;
    private             Window      window;
    
    internal DrawGui(Draw2D draw, Batch2D batch) {
        this.draw   = draw;
        this.batch  = batch;
        window      = null!;
    }
    
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
        
        var input       = batch.input;
        int parentHash  = window.GetCurrentScopeHash();
        int widgetId    = id.Resolve(name, parentHash);
        
        var size    = draw.MeasureString(name);
        var clicked = false;
        var isHover = window.IsHover(batch, size);

        if (isHover) {
            if (input.activeItem == 0) {
                input.hotItem = widgetId;
            }
        } else if (input.hotItem == widgetId) {
            input.hotItem = 0;
        }

        if (input.hotItem == widgetId && batch.input.isMouseDown) {
            input.activeItem = widgetId;
        }

        if (input.activeItem == widgetId) {
            if (!batch.input.isMouseDown)
            {
                if (input.hotItem == widgetId) {
                    clicked = true;
                }
                input.activeItem = 0;
            }
        }

        if (input.activeItem == widgetId) {
            color = window.buttonDown;
        }
        else if (input.hotItem == widgetId) {
            color = window.buttonHover;
        }
        
        draw.RectangleRounded(window.cursor, size, 8, color);
        draw.DrawStringInRect(name, window.cursor, size, TextAlignment.Center, VerticalAlignment.Middle, textColor);
        
        window.MoveCursor(size);
        return clicked;
    }
}