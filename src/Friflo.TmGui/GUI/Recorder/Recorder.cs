// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Collections.Generic;
using System.Diagnostics;
using Friflo.TmGui.TUI;
using System.Runtime.CompilerServices;
using Friflo.TmGui.Session;


// ReSharper disable ConvertIfStatementToReturnStatement
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;


internal readonly struct Record
{
    internal readonly   RecordType  type;
    internal readonly   int         index;

    public   override   string      ToString() => $"{type} - index: {index}";
    
    internal Record(RecordType type, int index)
    {
        this.type   = type;
        this.index  = index;
    }
}


internal sealed partial class GuiRecorder
{
    private             long                lastRecordTime;
    private             bool                rewindStack;
    private  readonly   TmBatch             batch;
    internal readonly   GuiReplay           replay;
    
    private  readonly   List<Record>        records         = [];
    private  readonly   List<Record>        stackEnd        = [];
    private  readonly   List<char>          textBuffer      = [];
    private  readonly   List<Color32>       colorBuffer     = [];

    
    internal GuiRecorder(TmBatch batch, GuiReplay replay)
    {
        this.replay = replay;
        this.batch  = batch;
    }
    
    private void PopStackEnd()
    {
        if (rewindStack) {
            return;
        }
        stackEnd.RemoveAt(stackEnd.Count - 1);
    }
    
    private void PushStackEnd(RecordType type, int index)
    {
        if (rewindStack) {
            return;
        }
        stackEnd.Add(new Record(type, index - 1));
    }
    
    private void AddCommand(RecordType type, int index)
    {
        if (rewindStack) {
            return;
        }
        records.Add(new Record(type, index - 1));
        
        var time = Stopwatch.GetTimestamp();
        var diff = Stopwatch.GetElapsedTime(lastRecordTime, time);
        lastRecordTime = time;
        if (diff.TotalMilliseconds < 100) {
            return;
        }
        Replay();
    }
    
    private void Replay()
    {
        var replayBatch = replay.batch;
        
        replay.backend.NewFrame();
        
        var replayGui = replayBatch.BeginGui(batch.beginWidth, batch.beginHeight);
        
        stackEnd.Clear();
        rewindStack = false;
        
        ReplayCommands(this, replayGui.widget, records);
        
        rewindStack = true;
        stackEnd.Reverse();
        
        ReplayCommands(this, replayGui.widget, stackEnd);
        
        rewindStack = false;
        
        replay.session.SendReplayCommands();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Color32Span GetColorSpan(in TextColor color)
    {
        Color32Span colorSpan;
        switch (color.kind) {
            case TmColorKind.Span:
                colorSpan = new Color32Span(colorBuffer.Count, color.colors.Length);
                colorBuffer.AddRange(color.colors);
                break;
            case TmColorKind.Value:
                colorSpan = new Color32Span(color.value);
                break;
            default:
            case TmColorKind.None:
                colorSpan = new Color32Span();
                break;
        }
        return colorSpan;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TextSpan GetTextSpan(ReadOnlySpan<char> text)
    {
        var span = new TextSpan { start = textBuffer.Count, len = text.Length };
        textBuffer.AddRange(text);
        return span;
    }
}


internal static class RecorderExtensions
{
    extension (ReadOnlySpan<char> buffer)
    {
        internal ReadOnlySpan<char> GetText(TextSpan span) {
            return buffer.Slice(span.start, span.len);
        }
    }
    extension (ReadOnlySpan<Color32> buffer)
    {
        internal TextColor GetColor(Color32Span span) {
            if (span.len == 0) {
                return new TextColor(span.value);
            }
            if (span.len == -1) {
                return new TextColor();
            }
            return new TextColor(buffer.Slice(span.start, span.len));
        }
    }
}

internal sealed class GuiReplay
{
    internal readonly   TmGuiBackend    backend;
    internal readonly   TmBatch         batch;
    internal readonly   TmSession       session;
    
    internal GuiReplay(TmGuiBackend backend, TmBatch batch, TmSession session) {
        this.backend    = backend;
        this.batch      = batch;
        this.session    = session;
    }
}
