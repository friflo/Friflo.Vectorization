// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Tasks;

// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.WebRTC;

internal record SignalingMessage(string type, string? sdpOrCandidate, string? host)
{
    public override string ToString() => $"{type} > host: {host}";
}

public partial class SignalingListener
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

    private async Task ProcessSignalingMessage(WebSocket webSocket, ReadOnlyMemory<byte> buffer)
    {
        var message = Parse(buffer.Span);
        
    }
}