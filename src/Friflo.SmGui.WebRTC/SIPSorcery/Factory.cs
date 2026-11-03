// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Text.Json;
using SIPSorcery.Net;

// ReSharper disable CheckNamespace
namespace Friflo.SmGui.WebRTC;


internal sealed class SipFactory : WebRtcFactory
{
    internal override WebRtcPeerConnection CreatePeerConnection()
    {
        return new SipRtcPeerConnection();
    }
    
    internal override WebRtcSessionDescriptionInit CreateSessionDescriptionInit(string? sdp, WebRtcSdpType type)
    {
        var native = new RTCSessionDescriptionInit {
            sdp  = sdp,
            type = (RTCSdpType)type
        };
        return new WebRtcSessionDescriptionInit { native = native, sdp = sdp };
    }
    
    internal override string CandidateToJson(WebRtcIceCandidate candidate)
    {
        var iceCandidate = (RTCIceCandidate)candidate.native;
        var json = JsonSerializer.Serialize(iceCandidate);
        return json;
    }

    internal override WebRtcIceCandidateInit JsonToCandidate(string json)
    {
        var candidateInit = JsonSerializer.Deserialize<RTCIceCandidateInit>(json);
        return new WebRtcIceCandidateInit { native = candidateInit };
    }
}