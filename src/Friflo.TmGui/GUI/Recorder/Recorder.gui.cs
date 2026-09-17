// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Friflo.TmGui.TUI;
using System.Numerics;
using System.Runtime.InteropServices;

// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable UseDeconstruction
// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;


internal readonly record struct WindowBegin (string title, Vector2? pos, Vector2? size, TmTrait traits, TuiBorder tuiBorder);
internal readonly record struct LayoutBegin (Dim size);


internal readonly record struct Label       (TextSpan name, Color32Span textColor);
internal readonly record struct Button      (TextSpan name, Dim size, GuiStyle? style, WidgetID id, Color32Span textColor);
internal readonly record struct Checkbox    (TextSpan name, bool value, GuiStyle? style, WidgetID id);
internal readonly record struct Slider      (TextSpan name, float value, float min, float max, float width, TextSpan format, GuiStyle? style, WidgetID id);

internal enum RecordType
{
    None,
    
    WindowBegin,        WindowEnd,
    HorizontalBegin,    HorizontalEnd,
    VerticalBegin,      VerticalEnd,
    
    Label,
    Button,
    Checkbox,
    Slider
}

internal sealed partial class GuiRecorder
{
    private readonly    List<WindowBegin>   windowBegin     = [];
    private readonly    List<WindowEnd>     windowEnd       = [];
    
    private readonly    List<LayoutBegin>   layoutBegin     = [];
    private readonly    List<RecordType>    layoutEnd       = [];
    
    private readonly    List<Label>         label           = [];
    private readonly    List<Button>        button          = [];
    private readonly    List<Checkbox>      checkbox        = [];
    private readonly    List<Slider>        slider          = [];


    
    internal void Reset()
    {
        lastRecordTime  = Stopwatch.GetTimestamp();
        recordsSendCount = 0;
        
        records.Clear();
        textBuffer.Clear();
        colorBuffer.Clear();
        
        // --- container
        windowBegin.Clear();
        windowEnd.Clear();
        
        layoutBegin.Clear();
        layoutEnd.Clear();
        
        // --- widgets
        label.Clear();
        button.Clear();
        checkbox.Clear();
        slider.Clear();
    }
    
    private static void ReplayCommands(GuiRecorder recorder, in GuiWidget widget, List<Record> replayList)
    {
        var replays         = CollectionsMarshal.AsSpan(replayList);
        //
        var textBuffer      = CollectionsMarshal.AsSpan(recorder.textBuffer);
        var colorBuffer     = CollectionsMarshal.AsSpan(recorder.colorBuffer);
        
        // --- container
        var windowBegin     = CollectionsMarshal.AsSpan(recorder.windowBegin);
        var windowEnd       = CollectionsMarshal.AsSpan(recorder.windowEnd);
        
        var layoutBegin     = CollectionsMarshal.AsSpan(recorder.layoutBegin);
        var layoutEnd       = CollectionsMarshal.AsSpan(recorder.layoutEnd);
        
        // --- widgets
        var label           = CollectionsMarshal.AsSpan(recorder.label);
        var button          = CollectionsMarshal.AsSpan(recorder.button);
        var checkbox        = CollectionsMarshal.AsSpan(recorder.checkbox);
        var slider          = CollectionsMarshal.AsSpan(recorder.slider);
        
        for (int n = 0; n < replays.Length; n++)
        {
            var record  = replays[n];
            var index   = record.index;
            var type    = record.type;
            switch (type)
            {
                // -------------------------------- containers -------------------------------
                // --- Window
                case RecordType.WindowBegin: {
                    WindowBegin cmd = windowBegin[index];
                    recorder.SyncWindow(cmd.title);
                    
                    var scope = widget.BeginWindow(cmd.title, cmd.pos, cmd.size, cmd.traits, cmd.tuiBorder);
                    recorder.windowEnd.Add(scope.end);
                    recorder.PushStackEnd(RecordType.WindowEnd, recorder.windowEnd.Count);
                    break;
                }
                case RecordType.WindowEnd: {
                    recorder.PopStackEnd();
                    
                    WindowEnd end = windowEnd[index];
                    widget.EndWindow(new WindowScope(widget, end));
                    break;
                }
                
                // --- Layout
                case RecordType.HorizontalBegin:
                case RecordType.VerticalBegin: {
                    LayoutBegin cmd = layoutBegin[index];
                    if (type == RecordType.HorizontalBegin) {
                        widget.BeginHorizontal(cmd.size);
                    } else {
                        widget.BeginVertical(cmd.size);
                    }
                    var end = type == RecordType.HorizontalBegin ? RecordType.HorizontalEnd : RecordType.VerticalEnd; 
                    recorder.layoutEnd.Add(end);
                    recorder.PushStackEnd(end, recorder.layoutEnd.Count);
                    break;
                }
                case RecordType.HorizontalEnd:
                case RecordType.VerticalEnd: {
                    recorder.PopStackEnd();
                    
                    if (type == RecordType.HorizontalEnd) {
                        widget.EndHorizontal();
                    } else {
                        widget.EndVertical();
                    }
                    break;
                }
                
                
                // ------------------------------- widgets -------------------------------
                case RecordType.Label: {
                    Label cmd = label[index];
                    widget.Label(textBuffer.GetText(cmd.name), colorBuffer.GetColor(cmd.textColor));
                    break;
                }
                case RecordType.Button: {
                    Button cmd = button[index];
                    widget.Button(textBuffer.GetText(cmd.name), cmd.size, cmd.style, cmd.id, colorBuffer.GetColor(cmd.textColor));
                    break;
                }
                case RecordType.Checkbox: {
                    Checkbox cmd = checkbox[index];
                    var value   = cmd.value;
                    widget.Checkbox(textBuffer.GetText(cmd.name), ref value, cmd.style, cmd.id);
                    break;
                }
                case RecordType.Slider: {
                    Slider cmd = slider[index];
                    var value   = cmd.value;
                    widget.Slider(textBuffer.GetText(cmd.name), ref value, cmd.min, cmd.max, cmd.width, textBuffer.GetText(cmd.format), cmd.style, cmd.id);
                    break;
                }
                default:
                case RecordType.None:
                    break;
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
    
    internal void BeginLayout(RecordType type, Dim size)
    {
        layoutBegin.Add(new LayoutBegin(size));
        AddCommand(type, layoutBegin.Count);
    }
    
    internal void EndLayout(RecordType type)
    {
        layoutEnd.Add(type);
        AddCommand(type, layoutEnd.Count);
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
    
    internal void Checkbox(ReadOnlySpan<char> name, bool value, GuiStyle? style, WidgetID id)
    {
        checkbox.Add(new Checkbox(GetTextSpan(name), value, style, id));
        AddCommand(RecordType.Checkbox, checkbox.Count);
    }
    
    internal void Slider(ReadOnlySpan<char> name, float value, float min, float max, float width, ReadOnlySpan<char> format, GuiStyle? style, WidgetID id)
    {
        slider.Add(new Slider(GetTextSpan(name), value, min, max, width, GetTextSpan(format), style, id));
        AddCommand(RecordType.Slider, slider.Count);
    }
}