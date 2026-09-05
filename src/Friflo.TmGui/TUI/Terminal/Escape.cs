// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
// ReSharper disable InconsistentNaming

namespace Friflo.TmGui.TUI.Terminal;

internal static class EscapeWrite
{
    public static   ReadOnlySpan<byte>  EnableRawTuiMode    => "\x1b[?1h\x1b[?25l"u8; // (Application Cursor Keys Mode) + (Hide Cursor)
    public static   ReadOnlySpan<byte>  ClearScreen         => "\x1b[2J\x1b[H"u8;
    
    public static   ReadOnlySpan<byte>  ResetAll            => "\x1b[0m"u8;   // Resets all colors, background, and text formatting (SGR 0)
}


internal static class Escape
{
    internal const byte     ESC = 0x1B;
    
    /// <summary> 0x5B ('['): Control Sequence Introducer.</summary>
    /// <remarks> Follows ESC to initiate ANSI sequences for cursor movement (Up/Down/Left/Right), color formatting (SGR), and mode toggles. </remarks>
    internal const byte     CSI = 0x5B;
    
    internal const byte     OSC = 0x5D;
}