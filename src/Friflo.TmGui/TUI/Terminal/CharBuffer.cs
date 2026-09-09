// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
// ReSharper disable ConvertToPrimaryConstructor


namespace Friflo.TmGui.TUI.Terminal;


internal struct CharBuffer
{
    private  readonly   char[]  array;
    private             int     length;
    private             int     current;

    public   override   string  ToString()      => $"\"{new string(array, 0, length)}\"  current: {new string(Remaining)}";
    
    [DebuggerHidden]
    internal            int     this[int index] => array[index];
    
    [DebuggerHidden]
    private ReadOnlySpan<char>  Remaining       => array.AsSpan(current,  length - current);
    
    [DebuggerHidden]
    internal char               Current         => array[current];


    public CharBuffer(int length) {
        array = new char[length];
    }
    
    internal void Reset()
    {
        length  = 0;
        current = 0;
    }
    
    internal void AppendChar(char c)    => array[length++] = c;
    
    internal void SkipFirst()           => current++;
    
    internal bool TryReadChar(int c)
    {
        if (Current != c) {
            return false;
        }
        current++;
        return true;
    }
    
    internal bool TryReadInt(out int value)
    {
        value = 0;
        for (int n = current; n < length; n++)
        {
            var c = array[n];
            if (char.IsDigit(c)) {
                value *= 10;
                value += c - '0';
                current++;
                continue;
            }
            return true;
        }
        return false;
    }
}