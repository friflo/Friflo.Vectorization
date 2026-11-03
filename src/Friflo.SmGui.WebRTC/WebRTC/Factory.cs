// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

// ReSharper disable CheckNamespace
namespace Friflo.SmGui.WebRTC;


internal abstract class WebRtcFactory
{
    internal static readonly WebRtcFactory Instance = new SipFactory();

    internal abstract   WebRtcPeerConnection            CreatePeerConnection();
    internal abstract   WebRtcSessionDescriptionInit    CreateSessionDescriptionInit(string? sdp, WebRtcSdpType type);
    
    internal abstract   string                          CandidateToJson(WebRtcIceCandidate candidate);
    internal abstract   WebRtcIceCandidateInit          JsonToCandidate(string json);
}