// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Collections.Generic;
using Friflo.TmGui.TUI;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;


// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;


internal readonly record struct WindowBegin (string title, Vector2? pos, Vector2? size, TmTrait traits, TuiBorder tuiBorder);

internal readonly record struct Button      (TextSpan name, Dim size, GuiStyle? style, WidgetID id, Color32Span textColor);


internal sealed class GuiRecorder
{
    private readonly    List<Record>        records         = [];
    private readonly    List<char>          textBuffer      = [];
    private readonly    List<Color32>       colorBuffer     = [];
    
    private readonly    List<WindowBegin>   windowBegin     = [];
    private readonly    List<WindowEnd>     windowEnd       = [];
    
    private readonly    List<Button>        button          = [];

    private enum RecordType
    {
        WindowBegin, WindowEnd,
        Button
    }
    
    private readonly struct Record(RecordType type, int index)
    {
        internal readonly   RecordType  type    = type;
        internal readonly   int         index   = index;
    }

    
    private void AddCommand(RecordType type, int index) {
        records.Add(new Record(type, index));
    }
    
    internal void Reset()
    {
        records.Clear();
        
        // --- container
        windowBegin.Clear();
        windowEnd.Clear();
        
        // --- widgets
        button.Clear();
    }
    
    private static void Replay(GuiRecorder recorder, in GuiWidget widget)
    {
        var records         = CollectionsMarshal.AsSpan(recorder.records);
        var textBuffer      = CollectionsMarshal.AsSpan(recorder.textBuffer);
        var colorBuffer     = CollectionsMarshal.AsSpan(recorder.colorBuffer);
        
        // --- container
        var windowBegin     = CollectionsMarshal.AsSpan(recorder.windowBegin);
        var windowEnd       = CollectionsMarshal.AsSpan(recorder.windowEnd);
        
        // --- widgets
        var button          = CollectionsMarshal.AsSpan(recorder.button);
        
        foreach (var record in records)
        {
            var index = record.index;
            switch (record.type) {
                case RecordType.WindowBegin: {
                    var cmd = windowBegin[index];
                    widget.BeginWindow(cmd.title, cmd.pos, cmd.size, cmd.traits, cmd.tuiBorder);
                    break;
                }
                case RecordType.WindowEnd: {
                    var end = windowEnd[index];
                    widget.EndWindow(new WindowScope(widget, end));
                    break;
                }
                // --- widgets
                case RecordType.Button: {
                    var cmd = button[index];
                    widget.Button(textBuffer.GetText(cmd.name), cmd.size, cmd.style, cmd.id, colorBuffer.GetColor(cmd.textColor));
                    break;
                }
            }
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Color32Span GetColorSpan(in TextColor color)
    {
        if (!color.IsSpan) {
            return new Color32Span(color.value);
        }
        var colorSpan = new Color32Span(colorBuffer.Count, color.colors.Length);
        colorBuffer.AddRange(color.colors);
        return colorSpan;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TextSpan GetTextSpan(ReadOnlySpan<char> text)
    {
        var span = new TextSpan { start = textBuffer.Count, len = text.Length };
        textBuffer.AddRange(text);
        return span;
    }
    
    
    // ------------------------------------- container
    internal void BeginWindow(in WindowBegin cmd)
    {
        windowBegin.Add(cmd);
        AddCommand(RecordType.WindowBegin, windowBegin.Count - 1);
    }
    
    internal void EndWindow(in WindowEnd cmd)
    {
        windowEnd.Add(cmd);
        AddCommand(RecordType.WindowEnd, windowEnd.Count - 1);
    }
    
    
    // ------------------------------------- widgets
    internal void Button(ReadOnlySpan<char> name, Dim size, GuiStyle? style, WidgetID id, in TextColor textColor)
    {
        button.Add(new Button(GetTextSpan(name), size, style, id, GetColorSpan(textColor)));
        AddCommand(RecordType.Button, button.Count - 1);
    }
}




internal static class RecorderExtensions
{
    extension (Span<char> buffer) {
        internal Span<char> GetText(TextSpan span) {
            return buffer.Slice(span.start, span.len);
        }
    }
    extension (Span<Color32> buffer) {
        internal TextColor GetColor(Color32Span span) {
            return buffer.Slice(span.start, span.len);
        }
    }
}