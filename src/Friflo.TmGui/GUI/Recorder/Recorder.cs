// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Collections.Generic;
using System.Diagnostics;
using Friflo.TmGui.TUI;
using System.Runtime.CompilerServices;

// ReSharper disable ConvertIfStatementToReturnStatement
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;


internal sealed partial class GuiRecorder
{
    private             long                lastRecordTime;
    private readonly    TmBatch             batch;
    
    private readonly    List<Record>        records         = [];
    private readonly    List<Record>        endRecords      = [];
    private readonly    List<char>          textBuffer      = [];
    private readonly    List<Color32>       colorBuffer     = [];
    
    private readonly struct Record
    {
        internal readonly   RecordType  type;
        internal readonly   int         index;
        internal readonly   int         beginIndex;

        public   override   string      ToString() => $"{type} - index: {index}";
        
        internal Record(RecordType type, int index)
        {
            this.type    = type;
            this.index   = index;
        }
        
        internal Record(RecordType type, int index, int beginIndex)
        {
            this.type       = type;
            this.index      = index;
            this.beginIndex = beginIndex;
        }
    }
    
    internal GuiRecorder(TmBatch batch)
    {
        this.batch = batch;
    }
    
    private void AddCommandEnd(RecordType type, int index, int beginIndex)
    {
        endRecords.Add(new Record(type, index - 1, beginIndex));
    }
    
    private void AddCommand(RecordType type, int index)
    {
        records.Add(new Record(type, index - 1));
        
        var time = Stopwatch.GetTimestamp();
        var diff = Stopwatch.GetElapsedTime(lastRecordTime, time);
        lastRecordTime = time;
        if (diff.TotalMilliseconds < 100) {
            return;
        }
        // Replay();
    }
    
    private void Replay()
    {
        TmBatch replayBatch = null!;
        var replayGui = replayBatch.BeginGui(batch.beginWidth, batch.beginHeight);
        
        ReplayCommands(this, replayGui.widget, records);
        
        ReplayCommands(this, replayGui.widget, endRecords);
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