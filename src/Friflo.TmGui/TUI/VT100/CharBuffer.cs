// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Hide = System.Diagnostics.DebuggerHiddenAttribute;

// ReSharper disable ConvertToPrimaryConstructor
namespace Friflo.TmGui.TUI.VT100;


internal struct CharBuffer
{
    private     char[]  array;
    private     int     length;
    private     int     current;

    public   override   string  ToString()      => $"\"{new string(array, 0, length)}\"  current: {new string(Remaining)}";
    
    [Hide] internal     int             this[int index] => array[index];
    [Hide] private ReadOnlySpan<char>   Remaining       => array.AsSpan(current,  length - current);
    [Hide] internal     char            Current         => array[current];


    public CharBuffer(int length) {
        array = new char[length];
    }
    
    internal void Reset()
    {
        length  = 0;
        current = 0;
    }
    
    internal bool HasMore => current < length;
    
    internal void MoveNext()
    {
        if (current < length) {
            current++;
        }
    }
    
    internal void AppendChar(char c)
    {
        if (length == array.Length) {
            var newArray = new char[length * 2];
            Array.Copy(array, 0, newArray, 0, length);
            array = newArray;
        }
        array[length++] = c;
    }

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