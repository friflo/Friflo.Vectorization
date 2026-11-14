using Friflo.SmGui;
using Friflo.SmGui.Recorder;

namespace TestConsole;

public static class GuiExtensions
{
    /// <summary>
    /// Example of a custom widget implementation
    /// </summary>
    public static bool MyButton(this in Gui gui, ReadOnlySpan<char> name, Dim size = default, GuiStyle? style = null, WidgetID id = default, in TextColor color = default)
    {
        var widget  = gui.widget;
        ButtonReplay.Record(widget.Recorder, name, size, style, id, color);
        
        var draw    = gui.Draw;
        var window  = widget.Window;
        using var _ = widget.UseStyle(style);
        
        int parentHash  = window.GetCurrentScopeHash();
        int widgetId    = id.Resolve(name, parentHash);
        
        var textSize    = draw.MeasureText(name);
        var finalSize   = textSize + widget.Sizes.FramePadding.Size;
        var pos         = window.Cursor;
        var isHover     = window.IsHoverAtCapture(pos, finalSize, draw);
        var isFocused   = widget.RegisterFocusable(widgetId, pos, finalSize);
        var widgetState = widget.GetWidgetState(isHover, widgetId);

        var tui = draw.Tui;
        if (tui != null) {
            tui.DrawButton(name, pos, finalSize, widget.Colors.ButtonText, widget.Colors.ButtonState(widgetState), isFocused);
        } else {
            draw.FillRectRounded  (pos, finalSize, widget.Sizes.CornerRadius, widget.Colors.ButtonState(widgetState), GuiSizes.CornerSegments); // background
            draw.StrokeRectRounded(pos, finalSize, widget.Sizes.CornerRadius, 2, widget.Colors.ButtonBorder, GuiSizes.CornerSegments);
            draw.DrawTextInRect(name, pos + widget.Sizes.FramePadding.Min, textSize, TextAlignment.Center, VerticalAlignment.Middle, widget.Colors.ButtonText);
        }
        if (isFocused) {
            widget.DrawFocus(pos, finalSize);
            widget.EnsureVisibleInScrollArea(pos, finalSize);
        }
        widget.MoveCursor(finalSize);
        return widget.IsFired(widgetState, isFocused);
    }
}
