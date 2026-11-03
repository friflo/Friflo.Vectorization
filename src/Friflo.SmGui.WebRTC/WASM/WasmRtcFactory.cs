// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Microsoft.JSInterop;


// ReSharper disable CheckNamespace
namespace Friflo.SmGui.WebRTC;


internal static class Wasm {
    internal const string JsModule = "webrtc-interop.js";
}

[SupportedOSPlatform("browser")]
internal sealed partial class WasmRtcFactory : RtcFactory, IDisposable
{
    private readonly    IJSObjectReference  jsModule;

    internal WasmRtcFactory(IJSRuntime jsRuntime)
    {
        // Load the JS interop module once at the factory level (needed for DotNetObjectReference callbacks)
        jsModule = jsRuntime.InvokeAsync<IJSObjectReference>("import", "./socket.js").GetAwaiter().GetResult();
    }
    
    public void Dispose()
    {
        jsModule.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    internal override PeerConnection CreatePeerConnection()
    {
        // Pass the shared JS module to the peer connection
        return new WasmPeerConnection(jsModule);
    }
    
    internal override SessionDescriptionInit CreateSessionDescriptionInit(string? sdp, SdpType type)
    {
        string typeStr = type.ToString().ToLowerInvariant();
        var jsInit = CreateSessionDescription(typeStr, sdp);
        return new SessionDescriptionInit { native = jsInit, sdp = sdp };
    }
    
    internal override string CandidateToJson(IceCandidate candidate)
    {
        var jsCandidate = (JSObject)candidate.native;
        return CandidateToString(jsCandidate);
    }

    internal override IceCandidateInit JsonToCandidate(string json)
    {
        var jsInit = ParseJsonToObj(json);
        return new IceCandidateInit { native = jsInit };
    }

    [JSImport("createSessionDescription", Wasm.JsModule)]
    private static partial JSObject CreateSessionDescription(string type, string? sdp);

    [JSImport("candidateToJson", Wasm.JsModule)]
    private static partial string CandidateToString(JSObject candidate);

    [JSImport("parseJsonToObj", Wasm.JsModule)]
    private static partial JSObject ParseJsonToObj(string json);
}