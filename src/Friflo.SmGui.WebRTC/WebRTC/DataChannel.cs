// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;

// ReSharper disable CheckNamespace
// ReSharper disable InconsistentNaming
namespace Friflo.SmGui.WebRTC;


/// <see cref="SIPSorcery.Net.RTCDataChannelState"/>
internal enum DataChannelState
{
    /// <summary>
    /// The user agent is attempting to establish the underlying data transport.
    /// This is the initial state of an RTCDataChannel object, whether created
    /// with createDataChannel, or dispatched as a part of an RTCDataChannelEvent.
    /// </summary>
    connecting,
    /// <summary> The underlying data transport is established and communication is possible. </summary>
    open,
    /// <summary> The procedure to close down the underlying data transport has started. </summary>
    closing,
    /// <summary> The underlying data transport has been closed or could not be established. </summary>
    closed,
}

/// <see cref="SIPSorcery.Net.DataChannelPayloadProtocols"/>
internal enum DataChannelPayloadProtocols
{
    WebRTC_DCEP             = 50, // 0x00000032
    WebRTC_String           = 51, // 0x00000033
    WebRTC_Binary_Partial   = 52, // 0x00000034
    WebRTC_Binary           = 53, // 0x00000035
    WebRTC_String_Partial   = 54, // 0x00000036
    WebRTC_String_Empty     = 56, // 0x00000038
    WebRTC_Binary_Empty     = 57, // 0x00000039
}


/// <see cref="SIPSorcery.Net.OnDataChannelMessageDelegate"/>
internal delegate void OnDataChannelMessage(DataChannel dc, DataChannelPayloadProtocols protocol, byte[] data);



/// <see cref="SIPSorcery.Net.RTCDataChannel"/>
internal abstract class DataChannel
{
    internal abstract DataChannelState  readyState { get; }
    internal abstract bool              ordered    { get; set; }
    internal abstract string            label      { get; set; }
    
    internal abstract event OnDataChannelMessage    onmessage;
    internal abstract event Action                  onclose;

    
    internal abstract void send(string message);
    internal abstract void send(byte[] data, int offset, int length);
}