// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

// ReSharper disable CheckNamespace
namespace Friflo.SmGui.WebRTC;


internal abstract class WebRtcFactory
{
    internal abstract   WebRtcPeerConnection    CreatePeerConnection();
    
    internal abstract   string                  CandidateToJson(WebRtcIceCandidate candidate);
    internal abstract   WebRtcIceCandidateInit  JsonToCandidate(string json);
}