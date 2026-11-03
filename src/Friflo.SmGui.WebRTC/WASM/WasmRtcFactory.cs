// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Text.Json;
using Microsoft.JSInterop;


// ReSharper disable CheckNamespace
namespace Friflo.SmGui.WebRTC;


internal sealed class WasmRtcFactory : RtcFactory, IDisposable
{
    private readonly    IJSRuntime          jsRuntime;
    private readonly    IJSObjectReference  jsModule;

    internal WasmRtcFactory(IJSRuntime jsRuntime)
    {
        this.jsRuntime = jsRuntime;
        
        // Load the JS interop module once at the factory level
        jsModule = jsRuntime.InvokeAsync<IJSObjectReference>("import", "./webrtcInterop.js").GetAwaiter().GetResult();
    }
    

    public void Dispose()
    {
        jsModule.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    internal override PeerConnection CreatePeerConnection()
    {
        // Pass the shared JS module to the peer connection
        return new WasmPeerConnection(jsRuntime, jsModule);
    }
    
    internal override SessionDescriptionInit CreateSessionDescriptionInit(string? sdp, SdpType type)
    {
        string typeStr = type.ToString().ToLowerInvariant();
        var jsInit = jsRuntime.InvokeAsync<IJSObjectReference>("eval", $"({{\"type\": \"{typeStr}\", \"sdp\": {JsonSerializer.Serialize(sdp)}}})").GetAwaiter().GetResult();
        return new SessionDescriptionInit { native = jsInit, sdp = sdp };
    }
    
    internal override string CandidateToJson(IceCandidate candidate)
    {
        var jsCandidate = candidate.native;
        return jsRuntime.InvokeAsync<string>("JSON.stringify", jsCandidate).GetAwaiter().GetResult();
    }

    internal override IceCandidateInit JsonToCandidate(string json)
    {
        var escapedJson = JsonSerializer.Serialize(json);
        var jsInit = jsRuntime.InvokeAsync<IJSObjectReference>("eval", $"JSON.parse({escapedJson})").GetAwaiter().GetResult();
        return new IceCandidateInit { native = jsInit };
    }
}