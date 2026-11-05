// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

#if !RTC_BROWSER

using System;
using SIPSorcery.Net;

// ReSharper disable CheckNamespace
namespace Friflo.SmGui.WebRTC;


internal sealed class SipDataChannel : DataChannel, IDisposable
{
    private readonly    RTCDataChannel                  native;
    private readonly    OnDataChannelMessageDelegate    dataChannelMessageHandler;
    private readonly    Action                          closeHandler;
        
    internal override   DataChannelState    readyState  => (DataChannelState)native.readyState;
    internal override   bool                ordered     { get => native.ordered; set => native.ordered = value; }
    internal override   string              label       { get => native.label;   set => native.label = value; }

    public   override   string?             ToString() => native.ToString();

    internal SipDataChannel(RTCDataChannel native) {
        this.native = native;
        dataChannelMessageHandler = (_, protocol, data) => {
            RaiseMessage((DataChannelPayloadProtocols)protocol, data);
        };
        closeHandler = RaiseClose;

        native.onmessage    += dataChannelMessageHandler;
        native.onclose      += closeHandler;
    }
    
    public void Dispose()
    {
        native.onmessage    -= dataChannelMessageHandler;
        native.onclose      -= closeHandler;
    }
    
    internal override void send(string message)
    {
        native.send(message);
    }
    
    internal override void send(byte[] data, int offset, int length)
    {
        native.send(data, offset, length);
    }
}

#endif