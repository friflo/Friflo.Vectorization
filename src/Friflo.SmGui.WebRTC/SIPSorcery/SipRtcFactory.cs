// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Text.Json;
using SIPSorcery.Net;

// ReSharper disable CheckNamespace
namespace Friflo.SmGui.WebRTC;


internal sealed class SipRtcFactory : RtcFactory
{
    internal override PeerConnection CreatePeerConnection()
    {
        return new SipRtcPeerConnection();
    }
    
    internal override SessionDescriptionInit CreateSessionDescriptionInit(string? sdp, SdpType type)
    {
        var native = new RTCSessionDescriptionInit {
            sdp  = sdp,
            type = (RTCSdpType)type
        };
        return new SessionDescriptionInit { native = native, sdp = sdp };
    }
    
    internal override string CandidateToJson(IceCandidate candidate)
    {
        var iceCandidate = (RTCIceCandidate)candidate.native;
        var json = JsonSerializer.Serialize(iceCandidate);
        return json;
    }

    internal override IceCandidateInit JsonToCandidate(string json)
    {
        var candidateInit = JsonSerializer.Deserialize<RTCIceCandidateInit>(json);
        return new IceCandidateInit { native = candidateInit };
    }
}