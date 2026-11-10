// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;

// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.RTC;

/// <see cref="SIPSorcery.Net.RTCSdpType"/>
internal enum SdpType
{
    answer,
    offer,
    pranswer,
    rollback,
}

internal struct SessionDescriptionInit
{
    /// <see cref="SIPSorcery.Net.RTCSessionDescriptionInit"/>
    internal    object  native;
    internal    string? sdp;
    
    public override string? ToString() => native?.ToString();
}

internal struct IceCandidateInit
{
    /// <see cref="SIPSorcery.Net.RTCIceCandidateInit"/>
    internal    object? native;
    
    public override string? ToString() => native?.ToString();
}


internal struct IceCandidate
{
    /// <see cref="SIPSorcery.Net.RTCIceCandidate"/>
    internal    object  native;

    public override string? ToString() => native.ToString();
}


/// <see cref="SIPSorcery.Net.RTCPeerConnection"/>
internal abstract class PeerConnection
{
    internal event  Action<IceCandidate>?   onicecandidate;

    protected void RaiseIceCandidate(IceCandidate candidate)    => onicecandidate?.Invoke(candidate);
    
    internal abstract   Task<SessionDescriptionInit>    createAnswer();
    internal abstract   Task<DataChannel>               createDataChannel(string label, int id);
    internal abstract   Task                            setLocalDescription(SessionDescriptionInit init);
    internal abstract   Task<int>                       setRemoteDescription(SessionDescriptionInit init);
    internal abstract   Task                            addIceCandidate(IceCandidateInit init);
}