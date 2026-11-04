// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;

// ReSharper disable CheckNamespace
namespace Friflo.SmGui.WebRTC;


internal abstract class RtcFactory
{
    internal static readonly RtcFactory  Instance = CreateFactory();
    
    private static RtcFactory CreateFactory()
    {
        // if (RuntimeInformation.ProcessArchitecture == Architecture.Wasm)
        if (OperatingSystem.IsBrowser()) {
            return new WasmRtcFactory();
        }
        return new SipRtcFactory();
    }

    internal abstract   PeerConnection          CreatePeerConnection();
    internal abstract   SessionDescriptionInit  CreateSessionDescriptionInit(string? sdp, SdpType type);
    
    internal abstract   string                  CandidateToJson(IceCandidate candidate);
    internal abstract   IceCandidateInit        JsonToCandidate(string json);
}