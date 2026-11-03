// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SIPSorcery.Net;

// ReSharper disable CheckNamespace
namespace Friflo.SmGui.WebRTC;


internal sealed class SipRtcPeerConnection : WebRtcPeerConnection
{
    private readonly RTCPeerConnection native;
    
    internal SipRtcPeerConnection() {
        native = new RTCPeerConnection();
    }
    
    private readonly Dictionary<Action<WebRtcDataChannel>, Action<RTCDataChannel>> onDataChannelHandler = new();
    
    internal override event Action<WebRtcDataChannel> ondatachannel
    {
        add {
            var handler = new Action<RTCDataChannel>(sipChannel => {
                var channel = new SipRtcDataChannel(sipChannel);
                value.Invoke(channel);
            });
            onDataChannelHandler.Add(value, handler);
            native.ondatachannel += handler;
        }
        remove {
            var handler = onDataChannelHandler[value];
            onDataChannelHandler.Remove(value);
            native.ondatachannel -= handler;
        }
    }
    
    
    private readonly Dictionary<Action<WebRtcIceCandidate>, Action<RTCIceCandidate>> onIceCandidateHandler = new();
    
    internal override event Action<WebRtcIceCandidate> onicecandidate
    {
        add {
            var handler = new Action<RTCIceCandidate>(sipCandidate => {
                var candidate = new WebRtcIceCandidate { native = sipCandidate };
                value.Invoke(candidate);
            });
            onIceCandidateHandler.Add(value, handler);
            native.onicecandidate += handler;
        }
        remove {
            var handler = onIceCandidateHandler[value];
            onIceCandidateHandler.Remove(value);
            native.onicecandidate -= handler;
        }
    }
    
    internal override WebRtcSessionDescriptionInit createAnswer()
    {
        var sipInit = native.createAnswer();
        return new WebRtcSessionDescriptionInit {
            native  = sipInit,
            type    = (WebRtcSdpType)sipInit.type,
            sdp     = sipInit.sdp
        };
    }
    
    internal override Task setLocalDescription(WebRtcSessionDescriptionInit init)
    {
        var sipInit = (RTCSessionDescriptionInit)init.native;
        return native.setLocalDescription(sipInit);
    }
        
    internal override int setRemoteDescription(WebRtcSessionDescriptionInit init)
    {
        var sipInit = (RTCSessionDescriptionInit)init.native;
        return (int)native.setRemoteDescription(sipInit);
    }
    
    internal override void addIceCandidate(WebRtcIceCandidateInit init)
    {
        var sipInit = (RTCIceCandidateInit?)init.native;
        native.addIceCandidate(sipInit);
    }
}