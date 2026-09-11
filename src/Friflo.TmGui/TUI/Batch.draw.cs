// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;


// ReSharper disable UseWithExpressionToCopyStruct
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable ConvertIfStatementToConditionalTernaryExpression
namespace Friflo.TmGui.TUI;

public partial class TuiBatch
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FillRect(Vector2 position, Vector2 size, Color32 background)
    {
        tuiRects.Add(new TuiRect(position, size, new Color32Span(background), ' '));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FillRectChar(Vector2 position, Vector2 size, Color32 background, char fillChar, Color32 textColor)
    {
        Span<Color32> colors = stackalloc Color32[2];
        colors[0] = background;
        colors[1] = textColor;
        var colorSpan = new Color32Span(colorBuffer.Count, colors.Length);
        colorBuffer.AddRange(colors);
        tuiRects.Add(new TuiRect(position, size, colorSpan, fillChar));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Color32Span GetColorSpan(in TextColor color)
    {
        if (!color.IsSpan) {
            return new Color32Span(color.value);
        }
        var colorSpan = new Color32Span(colorBuffer.Count, color.colors.Length);
        colorBuffer.AddRange(color.colors);
        return colorSpan;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawText(ReadOnlySpan<char> text, TextStyle style, Vector2 position, in TextColor color)
    {
        var textSpan    = new TextSpan { start = textBuffer.Count, len = text.Length };
        var colorSpan   = GetColorSpan(color);
        tuiRects.Add(new TuiRect(textSpan, style, position, new Vector2(text.Length * charWidth, lineHeight), colorSpan));
        textBuffer.AddRange(text);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawChar(char character, TextStyle style, Vector2 position, Color32 color)
    {
        var textSpan = new TextSpan { start = textBuffer.Count, len = 1 };
        tuiRects.Add(new TuiRect(textSpan, style, position, new Vector2(charWidth, lineHeight), new Color32Span(color)));
        textBuffer.Add(character);
    }
    
    public Vector2 DrawLabel(ReadOnlySpan<char> text, Vector2 position, Color32 color)
    {
        DrawText(text, TextStyle.None, position, color);
        return new Vector2(lineHeight * text.Length, lineHeight);
    }
    
    public static TextStyle GetStyle(bool isFocused)
    {
        return isFocused ? TextStyle.Bold : TextStyle.None;
    }
    
    public void Button(ReadOnlySpan<char> text, Vector2 position, Vector2 size, in TextColor color, Color32 background, bool isFocused)
    {
        FillRect(position, size, background);
        DrawText(text, GetStyle(isFocused), position + new Vector2(charWidth, 0), color);
    }
    
    public void Checkbox(bool value, ReadOnlySpan<char> text, Vector2 position, Vector2 size, Color32 color, Color32 boxColor, bool isFocused)
    {
        var boxText = value ? "[x]" : "[ ]";
        var boxSize = new Vector2(3 * charWidth, lineHeight);
        var style = GetStyle(isFocused);
        FillRect(position, boxSize, boxColor);
        DrawText(boxText, style, position, color);
        
        DrawText(text, style, position + new Vector2(4 * charWidth, 0), color);
    }

    public void Slider(ReadOnlySpan<char> name, Vector2 position, Vector2 size, Vector2 fillSize, Color32 color, Color32 sliderColor, Color32 fillColor, bool isFocused)
    {
        FillRect(position, size,     sliderColor);
        FillRect(position, fillSize, fillColor);
        var offset = new Vector2((size.X - name.Length * charWidth) * 0.5f, 0);
        DrawText(name, GetStyle(isFocused), position + offset, color);
    }

    public void DrawScrollbar(Vector2 position, Vector2 size, Color32 background, Vector2 thumbPosition, Vector2 thumbSize, Color32 thumbColor, bool isHorizontal)
    {
        var thumbChar = isHorizontal ? '▄' : '█';
        FillRectChar(thumbPosition, thumbSize, background, thumbChar, thumbColor); 
        /*
        var trackChar = isHorizontal ? '─' : '│';
        FillRectChar(position, size, background, trackChar, thumbColor);
        FillRectChar(thumbPosition, thumbSize, background, thumbChar, thumbColor);  // ▄ ▀
        */
    }
    
    public void Space(Vector2 pos, Vector2 size)
    {
        tuiRects.Add(new TuiRect(pos, size, new Color32Span(0xaaaaaaff), ' '));
    }
    
    internal void DrawFocus(Vector2 pos, Vector2 size, Color32 color)
    {
        var height = Math.Max(1, (int)((size.Y + lineHeight) * yScale));
        const TextStyle bold = TextStyle.Bold;
        if (height == 1) {
            DrawChar(focusBorder.left,  bold, pos,                                      color);
            DrawChar(focusBorder.right, bold, pos + new Vector2(size.X - charWidth, 0), color);
            return;
        }
        var barSize = new Vector2(charWidth, height * lineHeight);
        var buttonColor = guiState.currentStyle.colors.ButtonColor;
        FillRect(pos,                                       barSize, buttonColor);
        FillRect(pos + new Vector2(size.X - charWidth, 0),  barSize, buttonColor);
        
        for (int n = 0; n < height; n++) {
            DrawChar('│', bold, pos,                                      color);
            DrawChar('│', bold, pos + new Vector2(size.X - charWidth, 0), color);
            pos.Y += lineHeight;
        }
    }
    
    internal void DrawWindowTitle(ReadOnlySpan<char> title, Vector2 pos, Vector2 size, in GuiColors colors, Color32 headerColor)
    {
        FillRect(pos, size, colors.WindowColor);
        FillRect(pos, new Vector2(size.X, LineHeight), headerColor);
        DrawText(title, TextStyle.None, pos + new Vector2(2 * CharWidth, 0), colors.TextColor);
    }
    
    internal void DrawWindowBorder(Vector2 pos, Vector2 size, in GuiColors colors)
    {
        var yOffset     = new Vector2(0, LineHeight);
        var xOffset     = new Vector2(charWidth, 0);
        var vertical    = new Vector2(charWidth, size.Y - 2 *   LineHeight);
        var horizontal  = new Vector2(size.X, 0) - xOffset;
        
        // left / right
        FillRectChar(pos + yOffset,                      vertical,   colors.WindowColor, '│', colors.WindowBorder);
        FillRectChar(pos + horizontal + yOffset,         vertical,   colors.WindowColor, '│', colors.WindowBorder);
        
        // bottom
        var bl = pos + yOffset + vertical - xOffset;
        FillRectChar(bl, horizontal + yOffset + xOffset, colors.WindowColor, '─', colors.WindowBorder);
        
        // corners
        DrawChar('╰', TextStyle.None, bl,               colors.WindowBorder);
        DrawChar('╯', TextStyle.None, bl + horizontal,  colors.WindowBorder);
    }
}
