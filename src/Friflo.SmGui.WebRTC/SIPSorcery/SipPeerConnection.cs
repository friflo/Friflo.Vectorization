// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

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
        native.ondatachannel += sipChannel => {
            var channel = new SipRtcDataChannel(sipChannel);
            RaiseDataChannel(channel);
        };
        
        native.onicecandidate += sipCandidate => {
            var candidate = new IceCandidate { native = sipCandidate };
            RaiseIceCandidate(candidate);
        };
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