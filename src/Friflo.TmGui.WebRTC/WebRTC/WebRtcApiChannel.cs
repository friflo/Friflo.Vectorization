// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using Friflo.TmGui.Session;
using SIPSorcery.Net;

// ReSharper disable InconsistentNaming
// ReSharper disable MergeIntoPattern
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.WebRTC;


internal class WebRtcApiChannel
{
    private readonly    RTCDataChannel  dataChannel;
    private readonly    TmSessionLoop   loop;

    internal WebRtcApiChannel(TmSessionLoop loop, RTCDataChannel dataChannel)
    {
        this.loop           = loop;
        this.dataChannel    = dataChannel;
        dataChannel.ordered = true;
        // dataChannel.maxPacketLifeTime = 1;
        // dataChannel.maxRetransmits = 0;
        dataChannel.onmessage   += OnMessage;
        dataChannel.onclose     += OnClose;
    }
   
    private readonly MemoryStream outputGZip = new(1000);
        
    private byte[] CompressMessage(ReadOnlySpan<byte> rawData, out int length)
    {
        outputGZip.Position = 0;
        outputGZip.SetLength(0);
        
        {
            using var gzipStream = new GZipStream(outputGZip, CompressionLevel.Fastest, leaveOpen: true);
            gzipStream.Write(rawData);
        }
        length = (int)outputGZip.Length;
        
        if (outputGZip.TryGetBuffer(out var buffer)) {
            var array = buffer.Array; 
            if (array != null) {
                return array;
            }
        }
        return outputGZip.ToArray();
    }
    
    // private byte[] sendBuffer = [];

    
    private int Send(TmSendMessage message)
    {
        if (message.IsEmpty) {
            return 0;
        }
        if (dataChannel.readyState != RTCDataChannelState.open) {
            return 0;
        }
        var messageSpan = message.buffer.Span;
        var sendBuffer = CompressMessage(messageSpan, out int  length);
        

        // RTCDataChannel.send(byte[] data, int offset, int count) does not store a reference to passed data.
        // It creates multiple small byte[1020] arrays to ensure they fit in an MTU (Maximum Transmission Unit, 1200 in SIPSorcery).
        dataChannel.send(sendBuffer, 0, length);
        
        return length;
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
        if (!loop.resources.stringToImage.TryGetValue(apiRequest.url, out var image)) {
            return;
        }
        
        dataChannel.send(image.GetAsPng());
    }
}

internal record struct ApiRequest(string requestId, string url, string method)
{
    public override string ToString() => $"{requestId}  urL: {url}";
    
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

