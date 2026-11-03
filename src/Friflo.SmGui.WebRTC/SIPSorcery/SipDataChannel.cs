// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using SIPSorcery.Net;

// ReSharper disable CheckNamespace
namespace Friflo.SmGui.WebRTC;


internal sealed class SipRtcDataChannel : DataChannel
{
    private readonly RTCDataChannel native;
        
    internal override   DataChannelState    readyState => (DataChannelState)native.readyState;
    internal override   bool                ordered    { get => native.ordered; set => native.ordered = value; }
    internal override   string              label      { get => native.label;   set => native.label = value; }

    public   override   string?             ToString() => native.ToString();

    internal SipRtcDataChannel(RTCDataChannel native) {
        this.native = native;
    }
    
    
    private readonly Dictionary<OnDataChannelMessage, OnDataChannelMessageDelegate> onMessageHandler = new();
    
    internal override event OnDataChannelMessage onmessage
    {
        add {
            var handler = new OnDataChannelMessageDelegate((dc, protocol, data) => {
                value.Invoke(this, (DataChannelPayloadProtocols)protocol, data);
            });
            onMessageHandler.Add(value, handler);
            native.onmessage += handler;
        }
        remove {
            var handler = onMessageHandler[value];
            onMessageHandler.Remove(value);
            native.onmessage -= handler;
        }
    }
    
    internal override event Action onclose
    {
        add    => native.onclose += value;
        remove => native.onclose -= value;
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