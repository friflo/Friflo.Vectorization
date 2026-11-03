// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SIPSorcery.Net;

// ReSharper disable CheckNamespace
namespace Friflo.SmGui.WebRTC;


internal sealed class SipRtcPeerConnection : PeerConnection
{
    private  readonly   RTCPeerConnection   native;
    
    public   override   string?             ToString() => native.ToString();
    
    internal SipRtcPeerConnection() {
        native = new RTCPeerConnection();
    }
    
    private readonly Dictionary<Action<DataChannel>, Action<RTCDataChannel>> onDataChannelHandler = new();
    
    internal override event Action<DataChannel> ondatachannel
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
    
    
    private readonly Dictionary<Action<IceCandidate>, Action<RTCIceCandidate>> onIceCandidateHandler = new();
    
    internal override event Action<IceCandidate> onicecandidate
    {
        add {
            var handler = new Action<RTCIceCandidate>(sipCandidate => {
                var candidate = new IceCandidate { native = sipCandidate };
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
    
    internal override SessionDescriptionInit createAnswer()
    {
        var sipInit = native.createAnswer();
        return new SessionDescriptionInit { native = sipInit, sdp = sipInit.sdp };
    }
    
    internal override Task setLocalDescription(SessionDescriptionInit init)
    {
        var sipInit = (RTCSessionDescriptionInit)init.native;
        return native.setLocalDescription(sipInit);
    }
        
    internal override int setRemoteDescription(SessionDescriptionInit init)
    {
        var sipInit = (RTCSessionDescriptionInit)init.native;
        return (int)native.setRemoteDescription(sipInit);
    }
    
    internal override void addIceCandidate(IceCandidateInit init)
    {
        var sipInit = (RTCIceCandidateInit?)init.native;
        native.addIceCandidate(sipInit);
    }
}