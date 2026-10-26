// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Text.Json;
using Friflo.SmGui.Session;
using SIPSorcery.Net;

// ReSharper disable InconsistentNaming
// ReSharper disable MergeIntoPattern
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.WebRTC;


internal sealed class WebRtcApiChannel
{
    private readonly    RTCDataChannel  dataChannel;
    private readonly    SmSessionLoop   loop;

    internal WebRtcApiChannel(SmSessionLoop loop, RTCDataChannel dataChannel)
    {
        this.loop           = loop;
        this.dataChannel    = dataChannel;
        dataChannel.ordered = true;
        // dataChannel.maxPacketLifeTime = 1;
        // dataChannel.maxRetransmits = 0;
        dataChannel.onmessage   += OnMessage;
        dataChannel.onclose     += OnClose;
    }
    
    private void OnClose()
    {
        // Console.WriteLine($"RTCDataChannel closed");
    }
    
    private void OnMessage (RTCDataChannel dc, DataChannelPayloadProtocols protocol, byte[] data)
    {
        // var request = System.Text.Encoding.UTF8.GetString(data);
        // Console.WriteLine($"Received from client: {debugMsg}");
        
        if (!ApiRequest.TryParse(data, out var apiRequest)) {
            return;
        }
        var uri = new Uri(apiRequest.url);

        var path = uri.PathAndQuery;
        if (!path.StartsWith("/textures/", StringComparison.Ordinal)) {
            SendResponse(apiRequest.requestId, 404, null, []);
            return;
        }
        var assetName = path.Substring("/textures/".Length);
        
        if (!loop.resources.TryGetImageResource(assetName, out var image)) {
            SendResponse(apiRequest.requestId, 404, null, []);
            return;
        }
        var body = image.GetAsPng();
        SendResponse(apiRequest.requestId, 200, "image/png", body);
    }
    
    private void SendResponse(string requestId, int status, string? contentType, byte[] body)
    {
        var response = new ApiResponse { requestId = requestId, status = status, contentType = contentType };
        var header = JsonSerializer.SerializeToUtf8Bytes(response);
        
        var payload = new byte[4 + header.Length + body.Length];
        
        BitConverter.TryWriteBytes(payload.AsSpan(0, 4), header.Length);

        header.CopyTo(payload.AsSpan(4));
        body.CopyTo(payload.AsSpan(4 + header.Length));
        
        dataChannel.send(payload);
    }
}

internal record struct ApiRequest(string requestId, string url)
{
    public override string ToString() => $"{requestId}  url: {url}";
    
    internal static bool TryParse(ReadOnlySpan<byte> buffer, out ApiRequest message)
    {
        try {
            message = JsonSerializer.Deserialize<ApiRequest>(buffer);
            return true;
        }
        catch (JsonException) {
            message = default;
            return false;
        }
    }
}

internal readonly record struct ApiResponse(string requestId, int status, string? contentType)
{
    public override string ToString() => $"{requestId}  status: {status}";
}

