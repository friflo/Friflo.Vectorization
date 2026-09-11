// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Numerics;

// ReSharper disable MergeIntoLogicalPattern
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable SwitchStatementHandlesSomeKnownEnumValuesWithDefault
// ReSharper disable InconsistentNaming
// ReSharper disable ConvertToPrimaryConstructor
namespace Friflo.TmGui.TUI.VT100;

internal enum RS
{
    Ground,
    ESC,
    CSI,
}


internal sealed partial class TuiSession
{
    private RS          readState;
    private CharBuffer  csi = new(32);
        
    public Memory<byte> ProcessInput(ReadOnlySpan<byte> input)
    {
        int pos = 0;
        var rs  = readState;
        
        while (pos < input.Length)
        {
            var character = (char)input[pos++];
            rs = ReadInput(rs, character);
        }
        readState = rs;
        return IterateTui();
    }
     
    private RS ReadInput(RS rs, char character)
    {
        switch (rs)
        {
            case RS.CSI:
                csi.AppendChar(character);
                if (0x40 <= character && character <= 0x7E) {
                    HandleCSI();
                    return RS.Ground;
                }
                break;
            
            case RS.ESC:
                if (character == '[') { // 0x5b
                    return RS.CSI;
                }
                break;
            
            case RS.Ground:
                if (character == Escape.ESC) { // 0x1B
                    return RS.ESC;
                }
                HandleCharacter(character);
                return RS.Ground;
        }
        return rs;
    }
    
    private void HandleCharacter(char character)
    {
        switch (character)
        {
            case '\t':  // Tab
                backend.AddEvent(new TmEvent(TmEventType.KeyDown, new KeyEvent { code = KeyCode.Tab, isDown = true }));
                return;
            case '\r':  // Enter
                backend.AddEvent(new TmEvent(TmEventType.KeyDown, new KeyEvent { code = KeyCode.Return, isDown = true }));
                return;
            case ' ':   // Space
                backend.AddEvent(new TmEvent(TmEventType.KeyDown, new KeyEvent { code = KeyCode.Space,  isDown = true }));
                return;
        }
    }
    
    private void HandleCSI()
    {
        switch (csi[0])
        {
            case 'A':       // 0x41     Arrow Up
                backend.AddEvent(new TmEvent(TmEventType.KeyDown, new KeyEvent { code = KeyCode.Up,     isDown = true }));
                break;
            case 'B':       // 0x42     Arrow Down
                backend.AddEvent(new TmEvent(TmEventType.KeyDown, new KeyEvent { code = KeyCode.Down,   isDown = true }));
                break;
            case 'C':       // 0x43     Arrow Right
                backend.AddEvent(new TmEvent(TmEventType.KeyDown, new KeyEvent { code = KeyCode.Right,  isDown = true }));
                break;
            case 'D':       // 0x44     Arrow Left
                backend.AddEvent(new TmEvent(TmEventType.KeyDown, new KeyEvent { code = KeyCode.Left,   isDown = true }));
                break;
            
            case '<':       // 0x3C     Mouse events
                HandleMouse();
                break;
        }
        csi.Reset();
    }
    
    private void HandleMouse()
    {
        csi.SkipFirst();
        csi.TryReadInt(out int type);

        csi.TryReadChar(';');
        csi.TryReadInt(out int x);
        csi.TryReadChar(';');
        csi.TryReadInt(out int y);
        
        
        char finalChar  = csi.Current;  // 'M' = Down, 'm' = Up / Motion
        var mousePos    = new Vector2(x * batch.CharWidth, y * batch.LineHeight);

        switch (type)
        {
            case 0:     // Left Click
            case 1:     // Middle Click
            case 2:     // Right Click
                var isDown = finalChar == 'M';
                var ev = isDown ? TmEventType.MouseButtonDown : TmEventType.MouseButtonUp;
                backend.AddEvent(new TmEvent(ev, mousePos));
                break;
            case 32:    // Drag Left
            case 33:    // Drag Middle
            case 34:    // Drag Right
            case 35:    // Mouse Move (Hover) sends 'm' as finalChar
                backend.AddEvent(new TmEvent(TmEventType.MouseMotion, mousePos));
                break;
            case 64:    // Scroll Wheel Up
                backend.AddEvent(new TmEvent(TmEventType.MouseWheel, mousePos) { wheel = new Vector2(0, +1) });
                break;
            case 65:    // Scroll Wheel Down
                backend.AddEvent(new TmEvent(TmEventType.MouseWheel, mousePos) { wheel = new Vector2(0, -1) });
                break;
        }
    }
    

}