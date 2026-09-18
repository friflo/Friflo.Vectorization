// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Numerics;
using Friflo.TmGui.TUI;


// ReSharper disable InconsistentNaming
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;


public readonly ref partial struct GuiWidget
{
    internal WindowScope BeginWindow(string title, Vector2? pos, Vector2? size, TmTrait traits, TuiBorder tuiBorder)
    {
        WindowBeginReplay.Record(Recorder, new WindowBegin(title, pos, size, traits, tuiBorder));
        
        var host = draw.batch.host;
        var tui  = draw.Tui;
        if (!host.windows.TryGetValue(title, out guiState.window!)) {
            var finalPos    = pos  ?? new Vector2( 50,  50);
            var finalSize   = size ?? new Vector2(300, 200);
            // Snap initial window size to discrete terminal grid.
            tui?.SnapPositionToGrid(ref finalPos);
            tui?.SnapExtentToGrid(ref finalSize);
            
            guiState.window = new GuiWindow(host, title) {
                bounds      = new RectVector2(finalPos, finalSize),
                traits      = traits,
                tuiBorder   = tuiBorder,
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
        
        if (tui != null && (traits.HasFlag(TmTrait.Shadow) || window.IsTopWindow)) {
            tui.DrawWindowShadow(window.Pos, window.Size);
        }
        // ensure every drawing is clipped
        draw.PushScissor(window.Pos,  window.Size);
        var titleOffset = new Vector2(0f, titleBarHeight);
        var innerSize   = Vector2.Max(Vector2.Zero, window.Size - titleOffset);
        var contentPos  = window.Pos + titleOffset; // + Sizes.WindowPadding.Min;
        var scissorPos  = contentPos;
        var scissorSize = innerSize;
        
        // Render background & titlebar
        if (tui != null) {
            tui.DrawWindowTitle(title, window.Pos, window.Size, Colors, traits, tuiBorder);
            scissorPos  += new Vector2(tui.CharWidth,     0);
            scissorSize -= new Vector2(tui.CharWidth * 2, tui.LineHeight);
        } else {
            var textPos = window.Pos + new Vector2(10f, (titleBarHeight - LineHeight) / 2f);
            var headerColor = Colors.ButtonState(titleState);
            draw.FillRectRounded(window.Pos,   window.Size,  Sizes.CornerRadius, Colors.WindowColor,     GuiSizes.CornerSegments);
            draw.FillRectRounded(window.Pos,   titleBarSize, Sizes.CornerRadius, headerColor,            GuiSizes.CornerSegments);
            draw.DrawText(title, textPos, Colors.TextColor);
        }
        draw.PushScissor(scissorPos,  scissorSize);
        var scrollRect  = PushScrollArea(parentHash, contentPos, innerSize, Sizes.WindowPadding);
        window.InitLayout(scrollRect.pos, scrollRect.size);

        return new WindowScope(this, new WindowEnd(true, parentHash, contentPos, innerSize));
    }
    
    internal void EndWindow(in WindowScope scope)
    {
        WindowEndReplay.Record(Recorder, scope.end, false);
        
        var window      = Window;
        window.state    = WindowState.Visible;
        var scrollSize  = window.CurrentLayout.maxSize + Sizes.WindowPadding.Size;
        
        draw.PopScissor();
        
        if (window.traits.Has(TmTrait.Border)) {
            var tui = draw.Tui;
            if (tui != null) {
                tui.DrawWindowBorder(window.Pos, window.Size, Colors, window.tuiBorder);
            } else {
                draw.StrokeRectRounded(window.Pos, window.Size,  Sizes.CornerRadius, 2, Colors.WindowBorder, GuiSizes.CornerSegments);
            }
        }
        PopScrollArea(scope.end.windowId, scope.end.startCursor, scope.end.outerSize, scrollSize, Colors.WindowColor, false);
        
        draw.PopScissor();
        draw.PopZIndex();
        window.ClearScope();
    }
}


// --------------------------------------------- Step-Rendering ---------------------------------------------
internal readonly record struct WindowBegin(string title, Vector2? pos, Vector2? size, TmTrait traits, TuiBorder tuiBorder);

internal sealed class WindowBeginReplay : CmdReplay<WindowBegin>
{
    protected internal override void Replay(in Replay replay, int index)
    {
        var cmd = commands[index];
        replay.recorder.SyncWindow(cmd.title);
        
        var scope = replay.widget.BeginWindow(cmd.title, cmd.pos, cmd.size, cmd.traits, cmd.tuiBorder);
        WindowEndReplay.Record(replay.recorder, scope.end, true);
    }
    
    internal static void Record(GuiRecorder? rec, WindowBegin cmd)
    {
        rec?.Record<WindowBeginReplay, WindowBegin>(cmd, false);
    }
}

internal sealed class WindowEndReplay : CmdReplay<WindowEnd>
{
    protected internal override void Replay(in Replay replay, int index)
    {
        replay.PopStackEnd();
        replay.widget.EndWindow(new WindowScope(replay.widget, commands[index]));
    }
    
    internal static void Record(GuiRecorder? rec, WindowEnd cmd, bool isPush)
    {
        rec?.Record<WindowEndReplay, WindowEnd>(cmd, isPush);
    }
}