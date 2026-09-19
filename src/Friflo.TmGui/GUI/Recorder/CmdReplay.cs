// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;
using Friflo.TmGui.TUI;


// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;


public abstract class CmdReplay
{
    protected internal abstract void Clear();
    protected internal abstract void Replay(in Replay widget, int index);
}

public abstract class CmdReplay<T> : CmdReplay where T : struct
{
    protected internal  T[]     commands = new T[4];
    internal            int     count;
    
    internal static readonly  int TypeIndex = CmdReplayUtils.NewType(typeof(T));
    
    protected internal sealed   override    void    Clear()     => count = 0;
    public                      override    string  ToString()  => $"{typeof(T).Name} - Count: {count}";
}



internal static class CmdReplayUtils {
    
    private static int _nextIndex;
    
    internal static int NewType(Type type)
    {
        Types[_nextIndex] = type;
        return _nextIndex++;
    }
    
    internal const int MaxWidgetType = 500;
    
    internal static readonly  Type[] Types = new Type[MaxWidgetType];
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
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TextColor GetColor(Color32Span span) {
        if (span.len == 0) {
            return new TextColor(span.value);
        }
        if (span.len == -1) {
            return new TextColor();
        }
        return new TextColor(colorBuffer.Slice(span.start, span.len));
    }
    
    public void PopStackEnd()
    {
        if (recorder.rewindStack) {
            return;
        }
        recorder.pushRecords.RemoveAt(recorder.pushRecords.Count - 1);
    }
}


