// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

#if !RTC_BROWSER

using System.Text.Json;
using SIPSorcery.Net;

// ReSharper disable CheckNamespace
namespace Friflo.SmGui.RTC.SIP;


internal sealed class SipRtcFactory : RtcFactory
{
    internal override PeerConnection CreatePeerConnection()
    {
        return new SipPeerConnection();
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
        var json = JsonSerializer.Serialize(iceCandidate, SipJson.Default.RTCIceCandidate);
        return json;
    }

    internal override IceCandidateInit JsonToCandidate(string json)
    {
        var candidateInit = JsonSerializer.Deserialize(json, SipJson.Default.RTCIceCandidateInit);
        return new IceCandidateInit { native = candidateInit };
    }
}

#endif
