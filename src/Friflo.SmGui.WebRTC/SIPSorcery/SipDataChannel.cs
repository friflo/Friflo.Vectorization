// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

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
        native.onmessage += (dc, protocol, data) => {
            RaiseMessage((DataChannelPayloadProtocols)protocol, data);
        };

        native.onclose += RaiseClose;
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