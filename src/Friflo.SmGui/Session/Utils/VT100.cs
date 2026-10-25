// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;

// ReSharper disable InconsistentNaming
// ReSharper disable CanSimplifyStringEscapeSequence
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.Session;


internal static class VT100
{
    // \x1b[?1006l  Disable SGR mouse tracking
    // \x1b[?1003l  Disable all-motion mouse tracking
    // \x1b[?1002l  Disable button-event mouse tracking
    // \x1b[?1000l  Disable normal mouse tracking
    // \x1b[?2004l  Disable bracketed paste
    // \x1b[?1h     Set Cursor Keys to Application Mode (expected by shell prompts)
    // \x1b[?1049l  Leave alternate screen buffer
    // \x1b[?25h    Show cursor
    // \x1bc        VT100 RIS (Reset to Initial State)
    internal static readonly ReadOnlyMemory<byte> TerminalReset =
        "\x1b[?1006l\x1b[?1003l\x1b[?1002l\x1b[?1000l\x1b[?2004l\x1b[?1h\x1b[?1049l\x1b[?25h\x1bc"u8.ToArray().AsMemory();
    
    
    
    internal static int WriteWindowSizeReport(Span<byte> span, short width, short height)
    {
        int pos = 0;
        span[pos++] = (byte)'\x1b';
        span[pos++] = (byte)'[';
        span[pos++] = (byte)'8';
        span[pos++] = (byte)';';

        pos += WriteDecimalBytes(span.Slice(pos), height);
        span[pos++] = (byte)';';

        pos += WriteDecimalBytes(span.Slice(pos), width);
        span[pos++] = (byte)'t';

        return pos;
    }

    private static int WriteDecimalBytes(Span<byte> span, short value)
    {
        int pos = 0;
        if (value >= 10000) span[pos++] = (byte)('0' + (value / 10000 % 10));
        if (value >= 1000)  span[pos++] = (byte)('0' + (value / 1000 % 10));
        if (value >= 100)   span[pos++] = (byte)('0' + (value / 100 % 10));
        if (value >= 10)    span[pos++] = (byte)('0' + (value / 10 % 10));
        span[pos++] = (byte)('0' + (value % 10));
        return pos;
    }
}