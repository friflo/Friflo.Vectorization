// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;


// ReSharper disable InconsistentNaming
namespace Friflo.TmGui.TUI.VT100;

internal static class Telnet
{
    // Telnet protocol command bytes
    internal const byte     IAC     = 255; // Interpret As Command
    internal const byte     DONT    = 254;
    internal const byte     DO      = 253;
    internal const byte     WONT    = 252;
    internal const byte     WILL    = 251;
    internal const byte     SB      = 250; // Subnegotiation Begin
    internal const byte     SE      = 240; // Subnegotiation End
    
    internal const byte     OptionNAWS = 31; // Window Size Option
}


internal sealed partial class TuiSession
{
    private             byte    pendingTelnetCmd;
    private             int     subNegIndex;
    private readonly    byte[]  subNegBuffer = new byte[16];
    private             bool    isTelnet;

    private RS HandleTelnet(byte data)
    {
        isTelnet = true;
        switch (data)
        {
            // 1. Escaped 0xFF byte in Telnet stream (Byte Stuffing: IAC IAC -> 0xFF)
            case Telnet.IAC:
                // Append literal 0xFF byte to input buffer if needed
                return RS.Ground;

            // 2. Start of Option Negotiation (3-byte commands)
            case Telnet.WILL:
            case Telnet.WONT:
            case Telnet.DO:
            case Telnet.DONT:
                pendingTelnetCmd = data;
                return RS.Telnet_Negotiation;

            // 3. Start of Subnegotiation (e.g. NAWS payload)
            case Telnet.SB:
                subNegIndex = 0;
                return RS.Telnet_SubNegotiation;

            // 4. Single-byte Telnet commands (NOP, GA, AYT, etc.)
            default:
                // Handled single-byte command, return to ground state
                return RS.Ground;
        }
    }

    // Complete Telnet state transition dispatcher
    private RS ProcessTelnetState(RS currentState, byte data)
    {
        switch (currentState)
        {
            case RS.Telnet_IAC:
                return HandleTelnet(data);

            case RS.Telnet_Negotiation:
                HandleOptionNegotiation(pendingTelnetCmd, data);
                return RS.Ground;

            case RS.Telnet_SubNegotiation:
                // Check for closing IAC SE sequence
                if (data == Telnet.SE)
                {
                    // If previous byte stored was IAC (255), remove it from payload before processing
                    if (subNegIndex > 0 && subNegBuffer[subNegIndex - 1] == Telnet.IAC) {
                        subNegIndex--;
                    }
                    var payload = subNegBuffer.AsSpan(0, subNegIndex);
                    ProcessSubnegotiationPayload(payload);
                    return RS.Ground;
                }
                // Collect payload bytes (skipping byte-stuffed duplicate IACs handled by stream)
                if (subNegIndex < subNegBuffer.Length) {
                    subNegBuffer[subNegIndex++] = data;
                }
                return RS.Telnet_SubNegotiation;

            default:
                return currentState;
        }
    }

    private void HandleOptionNegotiation(byte cmd, byte option)
    {
        // Auto-respond to NAWS request from client
        if (cmd == Telnet.WILL && option == Telnet.OptionNAWS)
        {
            // Send IAC DO NAWS to enable auto window-size updates
            AppendSpan([Telnet.IAC, Telnet.DO, Telnet.OptionNAWS]);
        }
    }

    private void ProcessSubnegotiationPayload(ReadOnlySpan<byte> payload)
    {
        // Parse Telnet NAWS (RFC 1073): [NAWS_OPTION_BYTE] [W1] [W0] [H1] [H0]
        if (payload.Length >= 5 && payload[0] == Telnet.OptionNAWS)
        {
            int width  = (payload[1] << 8) | payload[2];
            int height = (payload[3] << 8) | payload[4];

            if (width > 0 && height > 0) {
                SetFrameSize(width, height);
            }
        }
    }
}