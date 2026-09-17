// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Friflo.TmGui.TUI;
using System.Numerics;
using System.Runtime.InteropServices;

// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;


internal readonly record struct WindowBegin (string title, Vector2? pos, Vector2? size, TmTrait traits, TuiBorder tuiBorder);

internal readonly record struct Label       (TextSpan name, Color32Span textColor);
internal readonly record struct Button      (TextSpan name, Dim size, GuiStyle? style, WidgetID id, Color32Span textColor);


internal sealed partial class GuiRecorder
{
    private readonly    List<WindowBegin>   windowBegin     = [];
    private readonly    List<WindowEnd>     windowEnd       = [];
    
    private readonly    List<Label>         label           = [];
    private readonly    List<Button>        button          = [];

    private enum RecordType
    {
        None,
        
        WindowBegin, WindowEnd,
        
        Label,
        Button
    }
    
    internal void Reset()
    {
        lastRecordTime = Stopwatch.GetTimestamp();
        records.Clear();
        endRecords.Clear();
        
        // --- container
        windowBegin.Clear();
        windowEnd.Clear();
        
        // --- widgets
        label.Clear();
        button.Clear();
    }
    
    private static void ReplayCommands(GuiRecorder recorder, in GuiWidget widget, List<Record> replayList)
    {
        var replays         = CollectionsMarshal.AsSpan(replayList);
        //
        var records         = CollectionsMarshal.AsSpan(recorder.records);
        var textBuffer      = CollectionsMarshal.AsSpan(recorder.textBuffer);
        var colorBuffer     = CollectionsMarshal.AsSpan(recorder.colorBuffer);
        
        // --- container
        var windowBegin     = CollectionsMarshal.AsSpan(recorder.windowBegin);
        var windowEnd       = CollectionsMarshal.AsSpan(recorder.windowEnd);
        
        // --- widgets
        var label           = CollectionsMarshal.AsSpan(recorder.label);
        var button          = CollectionsMarshal.AsSpan(recorder.button);
        
        foreach (var record in replays)
        {
            var index = record.index;
            switch (record.type)
            {
                // --- containers
                case RecordType.WindowBegin: {
                    var cmd = windowBegin[index];
                    var scope = widget.BeginWindow(cmd.title, cmd.pos, cmd.size, cmd.traits, cmd.tuiBorder);
                    recorder.windowEnd.Add(scope.end);
                    recorder.AddCommandEnd(RecordType.WindowEnd, recorder.windowEnd.Count, index);
                    break;
                }
                case RecordType.WindowEnd: {
                    if (records[record.beginIndex].type == RecordType.None) return;
                    records[record.beginIndex] = default;
                    var end = windowEnd[index];
                    widget.EndWindow(new WindowScope(widget, end));
                    break;
                }
                // --- widgets
                case RecordType.Label: {
                    var cmd = label[index];
                    widget.Label(textBuffer.GetText(cmd.name), colorBuffer.GetColor(cmd.textColor));
                    break;
                }
                case RecordType.Button: {
                    var cmd = button[index];
                    widget.Button(textBuffer.GetText(cmd.name), cmd.size, cmd.style, cmd.id, colorBuffer.GetColor(cmd.textColor));
                    break;
                }
            }
        }
    }
    
    
    // ------------------------------------- container
    internal void BeginWindow(in WindowBegin cmd)
    {
        windowBegin.Add(cmd);
        AddCommand(RecordType.WindowBegin, windowBegin.Count);
    }
    
    internal void EndWindow(in WindowEnd cmd)
    {
        windowEnd.Add(cmd);
        AddCommand(RecordType.WindowEnd, windowEnd.Count);
    }
    
    // ------------------------------------- widgets
    internal void Label(ReadOnlySpan<char> name, TextColor textColor)
    {
        label.Add(new Label(GetTextSpan(name), GetColorSpan(textColor)));
        AddCommand(RecordType.Label, label.Count);
    }
        
    internal void Button(ReadOnlySpan<char> name, Dim size, GuiStyle? style, WidgetID id, in TextColor textColor)
    {
        button.Add(new Button(GetTextSpan(name), size, style, id, GetColorSpan(textColor)));
        AddCommand(RecordType.Button, button.Count);
    }
}