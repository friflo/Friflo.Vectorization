// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

// ReSharper disable CheckNamespace
namespace Friflo.SmGui.RTC;

#pragma warning disable CA1859  // Change return type of method 'CreateFactory' from 'RtcFactory' to '...' for improved performance

internal abstract class RtcFactory
{
    internal static readonly RtcFactory  Instance = CreateFactory();
    
    private static RtcFactory CreateFactory()
    {
#if RTC_BROWSER
        return new WASM.WasmRtcFactory();
#else        
        return new SIP.SipRtcFactory();
#endif
    }

    internal abstract   PeerConnection          CreatePeerConnection();
    internal abstract   SessionDescriptionInit  CreateSessionDescriptionInit(string? sdp, SdpType type);
    
    internal abstract   string                  CandidateToJson(IceCandidate candidate);
    internal abstract   IceCandidateInit        JsonToCandidate(string json);
}