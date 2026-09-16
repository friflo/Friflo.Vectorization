// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Collections.Generic;
using Friflo.TmGui.TUI;
using System.Numerics;
using System.Runtime.InteropServices;

// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;


internal readonly record struct BeginWindowPod(string title, Vector2? pos, Vector2? size, TmTrait traits, TuiBorder tuiBorder);


internal sealed class GuiRecorder
{
    private readonly    List<Command>           commands        = [];
    private readonly    List<BeginWindowPod>    beginWindow     = [];

    private enum CommandType
    {
        BeginWindow
    }
    
    private readonly struct Command(CommandType type, int index)
    {
        internal readonly   CommandType type    = type;
        internal readonly   int         index   = index;
    }
    
    private void AddCommand(CommandType type, int index) {
        commands.Add(new Command(type, index));
    }
    
    private void Reset()
    {
        commands.Clear();
        beginWindow.Clear();
    }
    
    private void Replay(in GuiWidget widget)
    {
        var commandSpan         = CollectionsMarshal.AsSpan(commands);
        var beginWindowSpan     = CollectionsMarshal.AsSpan(beginWindow);
        
        foreach (var command in commandSpan)
        {
            var index = command.index;
            switch (command.type) {
                case CommandType.BeginWindow:   widget.BeginWindow(beginWindowSpan[index]);     break; 
            }
        }
    }
    
    internal void BeginWindow(in BeginWindowPod pod)
    {
        AddCommand(CommandType.BeginWindow, beginWindow.Count);
        beginWindow.Add(pod);
    }
}