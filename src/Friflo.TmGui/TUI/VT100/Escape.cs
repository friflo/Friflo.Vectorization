// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


// ReSharper disable InconsistentNaming
namespace Friflo.TmGui.TUI.VT100;


internal static class Escape
{
    internal const char     ESC = (char)0x1B;
    
    /// <summary> 0x5B ('['): Control Sequence Introducer.</summary>
    /// <remarks> Follows ESC to initiate ANSI sequences for cursor movement (Up/Down/Left/Right), color formatting (SGR), and mode toggles. </remarks>
    internal const char     CSI = (char)0x5B;
    
    internal const char     OSC = (char)0x5D;
}