// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using SIPSorcery.Net;

// ReSharper disable CheckNamespace
namespace Friflo.SmGui.WebRTC;


internal sealed class SipPeerConnection : PeerConnection, IDisposable
{
    private  readonly   RTCPeerConnection       native;
    private  readonly   Action<RTCDataChannel>  dataChannelHandler;
    private  readonly   Action<RTCIceCandidate> iceCandidateHandler;
    
    public   override   string?                 ToString() => native.ToString();
    
    internal SipPeerConnection() {
        native = new RTCPeerConnection();
        dataChannelHandler = sipChannel => {
            var channel = new SipDataChannel(sipChannel);
            RaiseDataChannel(channel);
        };
        iceCandidateHandler = sipCandidate => {
            var candidate = new IceCandidate { native = sipCandidate };
            RaiseIceCandidate(candidate);
        };
        native.ondatachannel  += dataChannelHandler;
        native.onicecandidate += iceCandidateHandler;
    }
    
    public void Dispose()
    {
        native.ondatachannel  -= dataChannelHandler;
        native.onicecandidate -= iceCandidateHandler;
        native.Dispose();
    }
    
    internal override Task<SessionDescriptionInit> createAnswer()
    {
        var sipInit = native.createAnswer();
        var result = new SessionDescriptionInit { native = sipInit, sdp = sipInit.sdp };
        return Task.FromResult(result);
    }
    
    internal override Task setLocalDescription(SessionDescriptionInit init)
    {
        var sipInit = (RTCSessionDescriptionInit)init.native;
        return native.setLocalDescription(sipInit);
    }
        
    internal override Task<int> setRemoteDescription(SessionDescriptionInit init)
    {
        var sipInit = (RTCSessionDescriptionInit)init.native;
        var result = (int)native.setRemoteDescription(sipInit);
        return Task.FromResult(result);
    }
    
    internal override Task addIceCandidate(IceCandidateInit init)
    {
        var sipInit = (RTCIceCandidateInit?)init.native;
        native.addIceCandidate(sipInit);
        return Task.CompletedTask;
    }
}