// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Text.Json;

// ReSharper disable InconsistentNaming
namespace Friflo.TmGui.WebRTC.WebRTC;

internal record SignalingMessage(string type, string? sdpOrCandidate, string? host)
{
    public override string ToString() => $"{type} > host: {host}";
}

public partial class WebRtcListener
{
    
    private static SignalingMessage? Parse(ReadOnlySpan<byte> buffer)
    {
        try {
            return JsonSerializer.Deserialize<SignalingMessage>(buffer);
        }
        catch (JsonException) {
            return null;
        }
    }

    private void ProcessSignalingMessage(ReadOnlySpan<byte> buffer)
    {
        var message = Parse(buffer);
        
    }
}