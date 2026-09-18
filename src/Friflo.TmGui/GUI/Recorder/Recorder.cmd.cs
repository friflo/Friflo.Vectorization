// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Friflo.TmGui.TUI;

// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable ConvertIfStatementToReturnStatement
// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable StaticMemberInGenericType
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;

internal readonly struct ReplayRecord
{
    internal readonly   int     index;
    internal readonly   int     type;

    public   override   string  ToString() => $"{ReplayCommands.Types[type].Name} - index: {index}";
    
    internal ReplayRecord(int type, int index)
    {
        this.type   = type;
        this.index  = index;
    }
}


internal sealed partial class GuiRecorder
{
    private  readonly   List<ReplayRecord>  replayRecords   = [];
    internal readonly   List<ReplayRecord>  pushRecords     = [];
    private  readonly   CmdReplay?[]        replays         = new CmdReplay?[200];
    private             int                 maxTypeIndex;
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Record<TReplay, T>(in T cmd, bool isPush)
        where T : struct
        where TReplay : CmdReplay<T>, new()
    {
        if (rewindStack) {
            return;
        }
        var cmdReplay = (CmdReplay<T>?)replays[CmdReplay<T>.TypeIndex];
        if (cmdReplay == null) {
            cmdReplay = new TReplay();
            replays[CmdReplay<T>.TypeIndex] = cmdReplay;
            maxTypeIndex = Math.Max(maxTypeIndex, CmdReplay<T>.TypeIndex + 1);
        }
        
        var commands = cmdReplay.commands;
        var count = cmdReplay.count;
        if (count == commands.Length) {
            commands = new T[2 * count];
            Array.Copy(cmdReplay.commands, commands, count);
            cmdReplay.commands = commands;
        }
        
        commands[count] = cmd;
        if (isPush) {
            pushRecords.   Add(new ReplayRecord(CmdReplay<T>.TypeIndex, count));
        } else {
            replayRecords.Add(new ReplayRecord(CmdReplay<T>.TypeIndex, count));
        }
        cmdReplay.count = count + 1;
        
        var time = Stopwatch.GetTimestamp();
        var diff = Stopwatch.GetElapsedTime(lastRecordTime, time);
        lastRecordTime = time;
        if (diff.TotalMilliseconds < 100) {
            return;
        }
        Replay();
    }
    
    internal void Reset()
    {
        lastRecordTime  = Stopwatch.GetTimestamp();
        recordsSendCount = 0;
        
        // records.Clear();
        replayRecords.Clear();
        
        textBuffer.Clear();
        colorBuffer.Clear();
        
        for (int n = 0; n < maxTypeIndex; n++) {
            replays[n]?.Clear();
        }
    }
    
    private static void ReplayCommands(GuiRecorder recorder, in GuiWidget widget, List<ReplayRecord> replayList)
    {
        var textBuffer      = CollectionsMarshal.AsSpan(recorder.textBuffer);
        var colorBuffer     = CollectionsMarshal.AsSpan(recorder.colorBuffer);
        var records         = CollectionsMarshal.AsSpan(replayList);
        
        var replay  = new Replay(recorder, widget, textBuffer, colorBuffer);
        var replays = recorder.replays;
        
        for (int n = 0; n < records.Length; n++)
        {
            var record      = records[n];
            var cmdReplay   = replays[record.type];
            cmdReplay!.Replay(replay, record.index);
        }
    }
}



public readonly ref struct Replay
{
    public   readonly   GuiWidget               widget;
    private  readonly   ReadOnlySpan<char>      textBuffer;
    private  readonly   ReadOnlySpan<Color32>   colorBuffer;
    internal readonly   GuiRecorder             recorder;
    
    internal Replay(GuiRecorder recorder, GuiWidget widget, ReadOnlySpan<char> textBuffer, ReadOnlySpan<Color32> colorBuffer) {
        this.recorder       = recorder;
        this.widget         = widget;
        this.textBuffer     = textBuffer;
        this.colorBuffer    = colorBuffer;
    }
    
    public ReadOnlySpan<char> GetText(TextSpan span) {
        return textBuffer.Slice(span.start, span.len);
    }
    
    public void PopStackEnd()
    {
        if (recorder.rewindStack) {
            return;
        }
        recorder.pushRecords.RemoveAt(recorder.pushRecords.Count - 1);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal TextColor GetColor(Color32Span span) {
        if (span.len == 0) {
            return new TextColor(span.value);
        }
        if (span.len == -1) {
            return new TextColor();
        }
        return new TextColor(colorBuffer.Slice(span.start, span.len));
    }
}



internal abstract class CmdReplay
{
    protected internal abstract void Clear();
    protected internal abstract void Replay(in Replay widget, int index);
}

internal abstract class CmdReplay<T> : CmdReplay where T : struct
{
    internal    T[]     commands = new T[4];
    internal    int     count;
    
    internal static readonly  int TypeIndex = ReplayCommands.NewType(typeof(T));
    
    protected internal  override    void    Clear()     => count = 0;
    public              override    string  ToString()  => $"Count: {count}";
}

internal static class ReplayCommands {
    
    private static int _nextIndex;
    
    internal static int NewType(Type type)
    {
        Types[_nextIndex] = type;
        return _nextIndex++;
    }
    
    internal static readonly  Type[] Types = new Type[200];
}
