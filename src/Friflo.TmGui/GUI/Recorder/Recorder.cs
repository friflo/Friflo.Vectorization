// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Friflo.SmGui.TUI;


// ReSharper disable ConvertIfStatementToReturnStatement
// ReSharper disable once CheckNamespace
namespace Friflo.SmGui.Recorder;


public sealed partial class GuiRecorder
{
    private             long            lastRecordTime;
    internal            bool            rewindStack;
    private             int             replayCounter;
    internal            int             recordsSendCount;
    private  readonly   SmBatch         batch;
    internal readonly   GuiReplay       replay;
    
    private  readonly   List<char>      textBuffer  = [];
    private  readonly   List<Color32>   colorBuffer = [];

    public   override   string          ToString()  => $"replays: {replayCounter}";


    internal GuiRecorder(SmBatch batch, GuiReplay replay)
    {
        this.replay = replay;
        this.batch  = batch;
    }
    
    internal void SyncWindow(string title)
    {
        if (!batch.host.windows.TryGetValue(title, out var window)) {
            return;
        }
        if(!replay.batch.host.windows.TryGetValue(title, out var replayWindow)) {
            return;
        }
        replayWindow.bounds = window.bounds;
        
        var replayScrollStates = replayWindow.scrollStates;
        foreach (var (id, scrollState) in replayScrollStates) {
            if (!window.scrollStates.TryGetValue(id, out var srcScrollState)) {
                continue;
            }
            replayScrollStates[id] = scrollState with { offset = srcScrollState.offset };
        }
    }

    internal void Replay()
    {
        if (recordsSendCount == replayRecords.Count) {
            return;
        }
        recordsSendCount = replayRecords.Count;
        replayCounter++;
        var replayBatch = replay.batch;
        
        replay.backend.NewFrame();
        
        var replayGui = replayBatch.BeginGui(batch.beginWidth, batch.beginHeight);
        
        pushRecords.Clear();
        rewindStack = false;
        
        ReplayCommands(this, replayGui.widget, replayRecords);
        
        rewindStack = true;
        pushRecords.Reverse();
        
        ReplayCommands(this, replayGui.widget, pushRecords);
        
        rewindStack = false;
        
        replay.session.SendReplayCommands();
    }

    [DebuggerHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Color32Span GetColorSpan(in TextColor color)
    {
        Color32Span colorSpan;
        switch (color.kind) {
            case SmColorKind.Span:
                colorSpan = new Color32Span(colorBuffer.Count, color.colors.Length);
                colorBuffer.AddRange(color.colors);
                break;
            case SmColorKind.Value:
                colorSpan = new Color32Span(color.value);
                break;
            default:
            case SmColorKind.None:
                colorSpan = new Color32Span();
                break;
        }
        return colorSpan;
    }
    
    [DebuggerHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TextSpan GetTextSpan(ReadOnlySpan<char> text)
    {
        var span = new TextSpan { start = textBuffer.Count, len = text.Length };
        textBuffer.AddRange(text);
        return span;
    }
}


internal sealed class GuiReplay
{
    internal readonly   SmGuiBackend    backend;
    internal readonly   SmBatch         batch;
    internal readonly   SmSession       session;
    
    internal GuiReplay(SmGuiBackend backend, SmBatch batch, SmSession session) {
        this.backend    = backend;
        this.batch      = batch;
        this.session    = session;
    }
}
