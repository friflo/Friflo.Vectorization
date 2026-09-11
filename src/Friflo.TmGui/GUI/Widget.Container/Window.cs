// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Numerics;
using Friflo.TmGui.TUI;


// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;


public readonly ref partial struct GuiWidget
{
    internal WindowScope BeginWindow(string title, Vector2? pos, Vector2? size)
    {
        var host = draw.batch.host;
        var tui  = draw.Tui;
        if (!host.windows.TryGetValue(title, out guiState.window!)) {
            var finalPos    = pos  ?? new Vector2( 50,  50);
            var finalSize   = size ?? new Vector2(300, 200);
            // Snap initial window size to discrete terminal grid.
            tui?.SnapPositionToGrid(ref finalPos);
            tui?.SnapExtentToGrid(ref finalSize);
            
            guiState.window = new GuiWindow(host, title) {
                bounds = new RectVector2(finalPos, finalSize)
            };
            host.windows.Add(title, guiState.window);
            host.windowOrder.Add(guiState.window);
        }
        var window = Window;
        
        // Hit test whole window
        bool isWindowHovered = !input.IsDragActive && window.IsHoverAt(window.Pos, window.Size, draw);

        // Focus window on click (WITHOUT capturing activeItem)
        if (isWindowHovered && input.IsMouseDown) {
            // Note: Moving window to front here ensures that subsequent child widgets 
            //       in this same frame pass the IsTopWindowAt() check and process clicks immediately.
            host.SetTopWindow(window);
        }
        var zindex = (uint)host.windowOrder.IndexOf(window) + 1;  // +1, so 0 is background;
        draw.PushZIndex(zindex);
        
        window.ResetScope();
        int parentHash = window.GetCurrentScopeHash();

        // Process window resize
        int resizeId 	= WidgetID.CombineHash(parentHash, "__resize".GetHashCode());
        bool isResizing = window.ProcessResize(this, resizeId, tui != null ? LineHeight : LineHeight * 0.5f);
        

        // Process title bar drag (strictly blocked while resizing)
        float titleBarHeight = LineHeight;
        var titleBarSize     = new Vector2(window.Size.X, titleBarHeight);
        int titleBarId       = WidgetID.CombineHash(parentHash, "__titlebar".GetHashCode());

        bool isTitleHover = !isResizing && window.IsHoverAtCapture(window.Pos, titleBarSize, draw);
        var titleState    = GetDragState(isTitleHover, titleBarId);

        if (titleState == DragState.Down) {
            window.bounds = new RectVector2(window.Pos + input.MousePosDelta, window.Size);
        }

        // Render background & titlebar

        var headerColor = Colors.ButtonState(titleState);
        var fontHeight  = LineHeight;
        var textPos     = window.Pos + new Vector2(10f, (titleBarHeight - fontHeight) / 2f);

        if (tui != null) {
            tui.FillRect(window.Pos, window.Size, Colors.WindowColor);
            tui.DrawText(title, TextStyle.None, textPos, Colors.TextColor);
        } else {
            draw.FillRectRounded(window.Pos,   window.Size,  Sizes.CornerRadius, Colors.WindowColor,     GuiSizes.CornerSegments);
            draw.FillRectRounded(window.Pos,   titleBarSize, Sizes.CornerRadius, headerColor,            GuiSizes.CornerSegments);
            draw.StrokeRectRounded(window.Pos, window.Size,  Sizes.CornerRadius, 2, Colors.WindowBorder, GuiSizes.CornerSegments);
            draw.DrawText(title, textPos, Colors.TextColor);
        }
        // --- Push content scissor rect (clips everything below titlebar) ---
        var titleOffset = new Vector2(0f, titleBarHeight);
        var innerSize   = Vector2.Max(Vector2.Zero, window.Size - titleOffset);
        var contentPos  = window.Pos + titleOffset; // + Sizes.WindowPadding.Min;
        
        var scrollRect = PushScrollArea(parentHash, contentPos, innerSize, Sizes.WindowPadding);
        window.InitLayout(scrollRect.pos, scrollRect.size);

        draw.PushScissor(window.Pos + titleOffset, innerSize);
        return new WindowScope(this, true, parentHash, contentPos, innerSize);
    }
    
    internal void EndWindow(in WindowScope scope)
    {
        var window      = Window;
        window.state    = WindowState.Visible;
        var scrollSize  = window.CurrentLayout.maxSize + Sizes.WindowPadding.Size;
        
        PopScrollArea(scope.windowId, scope.startCursor, scope.outerSize, scrollSize, Colors.WindowColor);
        
        draw.PopScissor();
        draw.PopZIndex();
        window.ClearScope();
    }
}
