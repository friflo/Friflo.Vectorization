// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Collections.Generic;
using Friflo.TmGui.TUI;
using System.Numerics;
using System.Runtime.InteropServices;

// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;


internal readonly record struct BeginWindow(string title, Vector2? pos, Vector2? size, TmTrait traits, TuiBorder tuiBorder);


internal sealed class GuiRecorder
{
    private readonly    List<Record>        records        = [];
    private readonly    List<BeginWindow>   beginWindow     = [];

    private enum RecordType
    {
        BeginWindow
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
        beginWindow.Clear();
    }
    
    private static void Replay(GuiRecorder recorder, in GuiWidget widget)
    {
        var records         = CollectionsMarshal.AsSpan(recorder.records);
        var beginWindow     = CollectionsMarshal.AsSpan(recorder.beginWindow);
        
        foreach (var record in records)
        {
            var index = record.index;
            switch (record.type) {
                case RecordType.BeginWindow:
                    var cmd = beginWindow[index];
                    widget.BeginWindow(cmd.title, cmd.pos, cmd.size, cmd.traits, cmd.tuiBorder);
                    break; 
            }
        }
    }
    
    internal void Add(in BeginWindow cmd)
    {
        AddCommand(RecordType.BeginWindow, beginWindow.Count);
        beginWindow.Add(cmd);
    }
}