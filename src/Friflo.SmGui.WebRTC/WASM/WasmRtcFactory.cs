// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Text.Json;


// ReSharper disable CheckNamespace
namespace Friflo.SmGui.WebRTC;


internal sealed class WasmRtcFactory : RtcFactory
{
    internal override PeerConnection CreatePeerConnection()
    {
        return new WasmPeerConnection();
    }
    
    internal override SessionDescriptionInit CreateSessionDescriptionInit(string? sdp, SdpType type)
    {
        throw new NotImplementedException();
    }
    
    internal override string CandidateToJson(IceCandidate candidate)
    {
        throw new NotImplementedException();
    }

    internal override IceCandidateInit JsonToCandidate(string json)
    {
        throw new NotImplementedException();
    }
}