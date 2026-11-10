// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

#if !RTC_BROWSER

using System;
using SIPSorcery.Net;

// ReSharper disable CheckNamespace
namespace Friflo.SmGui.RTC.SIP;


internal sealed class SipDataChannel : DataChannel, IDisposable
{
    private readonly    RTCDataChannel                  native;
    private readonly    OnDataChannelMessageDelegate    dataChannelMessageHandler;
    private readonly    Action                          openHandler;
    private readonly    Action                          closeHandler;
        
    internal override   DataChannelState    readyState  => (DataChannelState)native.readyState;
    internal override   bool                ordered     => native.ordered;

    internal SipDataChannel(string label, RTCDataChannel native) : base(label)
    {
        this.native = native;
        dataChannelMessageHandler = (_, protocol, data) => {
            RaiseMessage((DataChannelPayloadProtocols)protocol, data);
        };
        openHandler  = RaiseOpen;
        closeHandler = RaiseClose;

        native.onopen       += openHandler;
        native.onmessage    += dataChannelMessageHandler;
        native.onclose      += closeHandler;
    }
    
    public void Dispose()
    {
        native.onopen       -= openHandler;
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