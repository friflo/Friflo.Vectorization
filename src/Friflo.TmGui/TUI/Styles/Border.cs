// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
// ReSharper disable UnusedMember.Global
namespace Friflo.TmGui.TUI;


public struct TuiBorder
{
    public  bool    useTitleBg;
    public  bool    isSet;
    public  char    left;
    public  char    right;
    public  char    top;
    public  char    bottom;
    public  char    TL;
    public  char    TR;
    public  char    BL;
    public  char    BR;
    
    public static readonly TuiBorder Outer = new() {
        isSet = true, useTitleBg = true,
        left = '▏', right = '▕',    top = ' ',  bottom = '▁',
        TL   = ' ', TR =    ' ',    BL= '▏',    BR = '▕'
    };
    
    public static readonly TuiBorder Rounded = new() {
        isSet = true, useTitleBg = false,
        left = '│', right = '│',    top = '─',  bottom = '─',
        TL   = '╭', TR =    '╮',    BL= '╰',    BR = '╯'
    };
    
    public static readonly TuiBorder Light = new() {
        isSet = true, useTitleBg = false,
        left = '│', right = '│', top = '─', bottom = '─',
        TL = '┌', TR = '┐', BL = '└', BR = '┘'
    };
    
    public static readonly TuiBorder Double = new() {
        isSet = true, useTitleBg = false,
        left = '║', right = '║', top = '═', bottom = '═',
        TL = '╔', TR = '╗', BL = '╚', BR = '╝'
    };
}
