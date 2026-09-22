// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Numerics;

// ReSharper disable InvertIf
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
    //
    Telnet_IAC,
    Telnet_Negotiation,
    Telnet_SubNegotiation
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
                if (character == '[')    { // 0x5b  CSI
                    return RS.CSI;
                }
                break;
            
            case RS.Ground:
                if (character == '\x1b') { // 0x1B  ECS
                    return RS.ESC;
                }
                if (character == (char)Telnet.IAC) {
                    return  RS.Telnet_IAC;
                }
                HandleCharacter(character);
                return RS.Ground;
            // 
            case RS.Telnet_IAC:
                return HandleTelnet((byte)character);
            
            case RS.Telnet_Negotiation:
            case RS.Telnet_SubNegotiation:
                return ProcessTelnetState(rs, (byte)character);
        }
        return rs;
    }
    
    private void HandleCharacter(char character)
    {
        switch (character)
        {
            case '\t':  // Tab
                tuiBackend.AddEvent(new TmEvent(TmEventType.KeyDown, new KeyEvent { code = KeyCode.Tab, isDown = true }));
                return;
            case '\r':  // Enter
                tuiBackend.AddEvent(new TmEvent(TmEventType.KeyDown, new KeyEvent { code = KeyCode.Return, isDown = true }));
                return;
            case ' ':   // Space
                tuiBackend.AddEvent(new TmEvent(TmEventType.KeyDown, new KeyEvent { code = KeyCode.Space,  isDown = true }));
                return;
        }
    }
    
    private void HandleCSI()
    {
        switch (csi[0])
        {
            case 'A':       // 0x41     Arrow Up
                tuiBackend.AddEvent(new TmEvent(TmEventType.KeyDown, new KeyEvent { code = KeyCode.Up,     isDown = true }));
                break;
            case 'B':       // 0x42     Arrow Down
                tuiBackend.AddEvent(new TmEvent(TmEventType.KeyDown, new KeyEvent { code = KeyCode.Down,   isDown = true }));
                break;
            case 'C':       // 0x43     Arrow Right
                tuiBackend.AddEvent(new TmEvent(TmEventType.KeyDown, new KeyEvent { code = KeyCode.Right,  isDown = true }));
                break;
            case 'D':       // 0x44     Arrow Left
                tuiBackend.AddEvent(new TmEvent(TmEventType.KeyDown, new KeyEvent { code = KeyCode.Left,   isDown = true }));
                break;
            
            case '<':       // 0x3C     Mouse events
                HandleMouse();
                break;
            case '4':       // 0x34     Terminal pixel size response
                HandlePixelSizeResponse();
                break;
            case '6':       // 0x36     Cell pixel size response (\x1b[6;<height>;<width>t)
                HandleCellPixelSizeResponse();
                break;
            case '8':       // 0x38     terminal resize
                HandleInBandResize();
                break;
            case '?':       // 0x3F     DA1 - Primary Device Attributes Response (\x1b[?...c)
                HandleDeviceAttributesResponse();
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
        var mousePos    = new Vector2(x * tuiBatch.CharWidth, y * tuiBatch.LineHeight);

        switch (type)
        {
            case 0:     // Left Click
            case 1:     // Middle Click
            case 2:     // Right Click
                var isDown = finalChar == 'M';
                var ev = isDown ? TmEventType.MouseButtonDown : TmEventType.MouseButtonUp;
                tuiBackend.AddEvent(new TmEvent(ev, mousePos));
                break;
            case 32:    // Drag Left
            case 33:    // Drag Middle
            case 34:    // Drag Right
            case 35:    // Mouse Move (Hover) sends 'm' as finalChar
                tuiBackend.AddEvent(new TmEvent(TmEventType.MouseMotion, mousePos));
                break;
            case 64:    // Scroll Wheel Up
                tuiBackend.AddEvent(new TmEvent(TmEventType.MouseWheel, mousePos) { wheel = new Vector2(0, +1) });
                break;
            case 65:    // Scroll Wheel Down
                tuiBackend.AddEvent(new TmEvent(TmEventType.MouseWheel, mousePos) { wheel = new Vector2(0, -1) });
                break;
        }
    }
    
    private void HandleDeviceAttributesResponse()
    {
        // Sequence format: \x1b[?<feature_1>;<feature_2>;...;<feature_n>c
        csi.SkipFirst(); // Skip '?'
        
        while (csi.HasMore)
        {
            if (csi.TryReadInt(out int featureCode)) {
                if (featureCode == 4) { // 4 = SIXEL Graphics
                    supportsSixel = true;
                }
            }
            // Advance past separator or stop at final character
            if (csi.Current == ';') {
                csi.MoveNext();
            } else if (csi.Current == 'c') {
                break;
            }
        }
    }
    
    private void HandleInBandResize()
    {
        // Sequence format: \x1b[8;<height>;<width>t
        csi.SkipFirst(); // Skip '8'
        
        csi.TryReadChar(';');
        csi.TryReadInt(out int height);
        csi.TryReadChar(';');
        csi.TryReadInt(out int width);

        char finalChar = csi.Current; // Must be 't'
        
        if (finalChar == 't' && width > 0 && height > 0) {
            SetFrameSize(width, height);
        }
    }

    private void HandlePixelSizeResponse()
    {
        // Sequence format: \x1b[4;<height>;<width>t
        csi.SkipFirst(); // Skip '4'
        
        csi.TryReadChar(';');
        csi.TryReadInt(out int heightPx);
        csi.TryReadChar(';');
        csi.TryReadInt(out int widthPx);

        char finalChar = csi.Current; // Must be 't'
        
        if (finalChar == 't' && widthPx > 0 && heightPx > 0) {
            // SetPixelSize(widthPx, heightPx);
        }
    }
    
    private void HandleCellPixelSizeResponse()
    {
        // Sequence format: \x1b[6;<height>;<width>t
        csi.SkipFirst(); // Skip '6'
        
        csi.TryReadChar(';');
        csi.TryReadInt(out int cellHeightPx);
        csi.TryReadChar(';');
        csi.TryReadInt(out int cellWidthPx);

        char finalChar = csi.Current; // Must be 't'
        
        if (finalChar == 't' && cellWidthPx > 0 && cellHeightPx > 0) {
            SetCellPixelSize(cellWidthPx, cellHeightPx);
        }
    }
}