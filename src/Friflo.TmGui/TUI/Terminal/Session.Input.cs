// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable SwitchStatementHandlesSomeKnownEnumValuesWithDefault

// ReSharper disable InconsistentNaming
// ReSharper disable ConvertToPrimaryConstructor
namespace Friflo.TmGui.TUI.Terminal;


internal enum RS
{
    New,
    ESC,
    CSI,
} 



public sealed partial class TuiSession
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
                    return RS.New;
                }
                break;
            
            case RS.ESC:
                if (character == '[') { // 0x5b
                    return RS.CSI;
                }
                break;
            
            case RS.New:
                switch (character)
                {
                    case Escape.ESC:    // 0x1B
                        return RS.ESC;
                    
                    case '\t':  // Tab
                        backend.AddEvent(new TmEvent(TmEventType.KeyDown, new KeyEvent { code = KeyCode.Tab, isDown = true }));
                        return RS.New;
                    case '\r':  // Enter
                        backend.AddEvent(new TmEvent(TmEventType.KeyDown, new KeyEvent { code = KeyCode.Return, isDown = true }));
                        return RS.New;
                    case ' ':   // Space
                        backend.AddEvent(new TmEvent(TmEventType.KeyDown, new KeyEvent { code = KeyCode.Space,  isDown = true }));
                        return RS.New;
                }
                break;
        }
        return rs;
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
            
            case '<':       // 0x3C     mouse
                csi.TryReadChar('<');
                csi.TryReadInt(out int type);
                csi.TryReadChar(';');
                csi.TryReadInt(out int x);
                csi.TryReadChar(';');
                csi.TryReadInt(out int y);
                break;
        }
        csi.length = 0;
    }
    

}