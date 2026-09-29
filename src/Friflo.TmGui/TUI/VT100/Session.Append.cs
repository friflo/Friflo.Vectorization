// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace Friflo.TmGui.TUI.VT100;


internal sealed partial class TuiSession
{
    private void AppendCursor(int x, int y)
    {
        AppendSpan("\x1b["u8);
        AppendNumber(y); // Row (Y)
        AppendAscii(';');
        AppendNumber(x); // Column (X)
        AppendAscii('H');
    }
    
    private void AppendColor(Color32 color)
    {
        AppendSpan("\x1b[38;2;"u8);
        AppendNumber(color.R);
        AppendAscii(';');
        AppendNumber(color.G);
        AppendAscii(';');
        AppendNumber(color.B);
        AppendAscii('m');
    }
    
    private void AppendBackground(Color32 background)
    {
        AppendSpan("\x1b[48;2;"u8);
        AppendNumber(background.R);
        AppendAscii(';');
        AppendNumber(background.G);
        AppendAscii(';');
        AppendNumber(background.B);
        AppendAscii('m');
    }

    // Allocation-free byte-to-ASCII integer formatting directly into send buffer
    private void AppendNumber(byte value)
    {
        var buffer = sendBuffer;
        if (value >= 100) {
            int d1 = value / 100;
            int rem = value % 100;
            buffer[sendBufferCount++] = (byte)('0' + d1);
            buffer[sendBufferCount++] = (byte)('0' + (rem / 10));
            buffer[sendBufferCount++] = (byte)('0' + (rem % 10));
        }
        else if (value >= 10) {
            buffer[sendBufferCount++] = (byte)('0' + (value / 10));
            buffer[sendBufferCount++] = (byte)('0' + (value % 10));
        }
        else {
            buffer[sendBufferCount++] = (byte)('0' + value);
        }
    }
    
    private void AppendNumber(int value)
    {
        var buffer = sendBuffer;
        if (value < 10) {
            buffer[sendBufferCount++] = (byte)('0' + value);
            return;
        }
        int digits = 1;
        int temp = value;
        while (temp >= 10) {
            temp /= 10;
            digits++;
        }
        sendBufferCount += digits;
        int index = sendBufferCount - 1;

        while (value >= 10) {
            int remainder = value % 10;
            buffer[index--] = (byte)('0' + remainder);
            value /= 10;
        }
        buffer[index] = (byte)('0' + value);
    }
    
    private void AppendAscii(char value)
    {
        sendBuffer[sendBufferCount++] = (byte)value; 
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AppendRune(Rune rune)
    {
        if (rune.Value == 0) {
            return; // Skip ghost cells. They follow runes which are two cells wide like 🙂
        }
        var destination = sendBuffer.AsSpan(sendBufferCount);
        int bytesWritten = rune.EncodeToUtf8(destination);
        sendBufferCount += bytesWritten;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AppendSpan(ReadOnlySpan<byte> buffer)
    {
        buffer.CopyTo(sendBuffer.AsSpan(sendBufferCount, buffer.Length));
        sendBufferCount += buffer.Length;
    }
    
    private void ApplyStyleDiff(TextStyle oldStyle, TextStyle newStyle)
    {
        var enabled  = newStyle & ~oldStyle;
        var disabled = oldStyle & ~newStyle;

        if ((enabled & TextStyle.Bold)          != 0) AppendSpan("\x1b[1m"u8);
        if ((enabled & TextStyle.Dim)           != 0) AppendSpan("\x1b[2m"u8);
        if ((enabled & TextStyle.Italic)        != 0) AppendSpan("\x1b[3m"u8);
        if ((enabled & TextStyle.Underline)     != 0) AppendSpan("\x1b[4m"u8);
        if ((enabled & TextStyle.Inverse)       != 0) AppendSpan("\x1b[7m"u8);
        if ((enabled & TextStyle.StrikeThrough) != 0) AppendSpan("\x1b[9m"u8);

        if ((disabled & TextStyle.Bold)         != 0) AppendSpan("\x1b[22m"u8);
        if ((disabled & TextStyle.Dim)          != 0) AppendSpan("\x1b[22m"u8);
        if ((disabled & TextStyle.Italic)       != 0) AppendSpan("\x1b[23m"u8);
        if ((disabled & TextStyle.Underline)    != 0) AppendSpan("\x1b[24m"u8);
        if ((disabled & TextStyle.Inverse)      != 0) AppendSpan("\x1b[27m"u8);
        if ((disabled & TextStyle.StrikeThrough)!= 0) AppendSpan("\x1b[29m"u8);
    }
}
