// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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

    public   override   string  ToString() => $"{CmdReplayUtils.Types[type].Name} - index: {index}";
    
    internal ReplayRecord(int type, int index)
    {
        this.type   = type;
        this.index  = index;
    }
}


public sealed partial class GuiRecorder
{
    private  readonly   List<ReplayRecord>  replayRecords   = [];
    internal readonly   List<ReplayRecord>  pushRecords     = [];
    private  readonly   CmdReplay?[]        cmdReplays      = new CmdReplay?[CmdReplayUtils.MaxWidgetType];
    private             int                 maxTypeIndex;
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Record<TReplay, T>(in T cmd, bool isPush)
        where T : struct
        where TReplay : CmdReplay<T>, new()
    {
        if (rewindStack) {
            return;
        }
        var cmdReplay = (CmdReplay<T>?)cmdReplays[CmdReplay<T>.TypeIndex];
        cmdReplay   ??= CreateCmdReplay<TReplay, T>();
        
        var commands = cmdReplay.commands;
        var count = cmdReplay.count;
        if (count == commands.Length) {
            commands = CreateCommands(cmdReplay);
        }
        commands[count] = cmd;
        
        cmdReplay.count = count + 1;

        var records = isPush ? pushRecords : replayRecords;
        records.Add(new ReplayRecord(CmdReplay<T>.TypeIndex, count));

        /*
        // using Gui.ToString() is sufficient
        var time = Stopwatch.GetTimestamp();
        var diff = Stopwatch.GetElapsedTime(lastRecordTime, time);
        lastRecordTime = time;
        if (diff.TotalMilliseconds < 100) {
            return;
        }
        Replay();
        */
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private TReplay CreateCmdReplay<TReplay, T>() where T : struct   where TReplay : CmdReplay<T>, new()
    {
        var cmdReplay = new TReplay();
        cmdReplays[CmdReplay<T>.TypeIndex] = cmdReplay;
        maxTypeIndex = Math.Max(maxTypeIndex, CmdReplay<T>.TypeIndex + 1);
        return cmdReplay;
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static T[] CreateCommands<T>(CmdReplay<T> cmdReplay) where T : struct
    {
        var count = cmdReplay.count;
        var commands = new T[2 * count];
        Array.Copy(cmdReplay.commands, commands, count);
        return cmdReplay.commands = commands;
    }
    
    internal void Reset()
    {
        lastRecordTime  = Stopwatch.GetTimestamp();
        recordsSendCount = 0;
        
        replayRecords.Clear();
        
        textBuffer.Clear();
        colorBuffer.Clear();
        
        var replays = cmdReplays;
        
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
        var replays = recorder.cmdReplays;
        
        for (int n = 0; n < records.Length; n++)
        {
            var record      = records[n];
            var cmdReplay   = replays[record.type];
            cmdReplay!.Replay(replay, record.index);
        }
    }
}
