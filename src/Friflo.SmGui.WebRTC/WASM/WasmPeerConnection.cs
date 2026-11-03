// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using SIPSorcery.Net;

// ReSharper disable CheckNamespace
namespace Friflo.SmGui.WebRTC;


internal sealed class WasmPeerConnection : PeerConnection, IDisposable
{
    private  readonly   RTCPeerConnection       native;
    private  readonly   Action<RTCDataChannel>  dataChannelHandler;
    private  readonly   Action<RTCIceCandidate> iceCandidateHandler;
    
    public   override   string?                 ToString() => native.ToString();
    
    internal WasmPeerConnection() {
        native = new RTCPeerConnection();
        dataChannelHandler = sipChannel => {
            var channel = new WasmDataChannel(sipChannel);
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