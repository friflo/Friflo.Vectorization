// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


// ReSharper disable CheckNamespace

using System;
using System.Threading.Tasks;

// ReSharper disable InconsistentNaming
namespace Friflo.SmGui.WebRTC;

/// <see cref="SIPSorcery.Net.RTCSdpType"/>
internal enum WebRtcSdpType
{
    answer,
    offer,
    pranswer,
    rollback,
}

internal struct WebRtcSessionDescriptionInit
{
    /// <see cref="SIPSorcery.Net.RTCSessionDescriptionInit"/>
    internal    object  native;
    internal    string? sdp;
    
    public override string? ToString() => native?.ToString();
}

internal struct WebRtcIceCandidateInit
{
    /// <see cref="SIPSorcery.Net.RTCIceCandidateInit"/>
    internal    object? native;
    
    public override string? ToString() => native?.ToString();
}


internal struct WebRtcIceCandidate
{
    /// <see cref="SIPSorcery.Net.RTCIceCandidate"/>
    internal    object  native;

    public override string? ToString() => native.ToString();
}


/// <see cref="SIPSorcery.Net.RTCPeerConnection"/>
internal abstract class WebRtcPeerConnection
{
    internal abstract   WebRtcSessionDescriptionInit    createAnswer();
    internal abstract   Task                            setLocalDescription(WebRtcSessionDescriptionInit init);
    internal abstract   int                             setRemoteDescription(WebRtcSessionDescriptionInit init);
    internal abstract   void                            addIceCandidate(WebRtcIceCandidateInit init);
    
    
    internal abstract event Action<WebRtcDataChannel>  ondatachannel;
    internal abstract event Action<WebRtcIceCandidate> onicecandidate;
}