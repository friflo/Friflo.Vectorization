// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Collections.Generic;
using Friflo.TmGui.TUI;
using System.Numerics;
using System.Runtime.InteropServices;

// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;


internal readonly record struct WindowBegin(string title, Vector2? pos, Vector2? size, TmTrait traits, TuiBorder tuiBorder);


internal sealed class GuiRecorder
{
    private readonly    List<Record>        records         = [];
    private readonly    List<WindowBegin>   windowBegin     = [];
    private readonly    List<WindowEnd>     windowEnd       = [];

    private enum RecordType
    {
        WindowBegin, WindowEnd,
    }
    
    private readonly struct Record(RecordType type, int index)
    {
        internal readonly   RecordType  type    = type;
        internal readonly   int         index   = index;
    }
    
    private void AddCommand(RecordType type, int index) {
        records.Add(new Record(type, index));
    }
    
    private void Reset()
    {
        records.Clear();
        windowBegin.Clear();
        windowEnd.Clear();
    }
    
    private static void Replay(GuiRecorder recorder, in GuiWidget widget)
    {
        var records         = CollectionsMarshal.AsSpan(recorder.records);
        var windowBegin     = CollectionsMarshal.AsSpan(recorder.windowBegin);
        var windowEnd       = CollectionsMarshal.AsSpan(recorder.windowEnd);
        
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
            }
        }
    }
    
    internal void Add(in WindowBegin cmd)
    {
        AddCommand(RecordType.WindowBegin, windowBegin.Count);
        windowBegin.Add(cmd);
    }
    
    internal void Add(in WindowEnd cmd)
    {
        AddCommand(RecordType.WindowEnd, windowEnd.Count);
        windowEnd.Add(cmd);
    }
}