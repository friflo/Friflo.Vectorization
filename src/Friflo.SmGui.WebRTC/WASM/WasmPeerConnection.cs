// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable CheckNamespace
namespace Friflo.SmGui.WebRTC;


[SupportedOSPlatform("browser")]
internal sealed partial class WasmPeerConnection : PeerConnection, IDisposable
{
    private static readonly Dictionary<int, WasmPeerConnection> Instances = new();
    private static          int                                 _nextId;

    private readonly    int         id;
    private readonly    JSObject    native;
    
    public override string? ToString() => native.ToString();
    
    internal WasmPeerConnection()
    {
        // Increment the static counter correctly to assign a unique ID
        id = Interlocked.Increment(ref _nextId);
        Instances[id] = this;

        native = CreatePeerConnection();
        
        // Pass the instance ID to JavaScript so it can route callbacks back via [JSExport]
        RegisterPeerConnectionCallbacks(id, native);
    }

    [JSImport("createPeerConnection", Wasm.JsModule)]
    private static partial JSObject CreatePeerConnection();

    [JSImport("registerPeerConnectionCallbacks", Wasm.JsModule)]
    private static partial void RegisterPeerConnectionCallbacks(int instanceId, JSObject pc);

    [JSExport]
    private static void HandleDataChannelCallback(int instanceId, JSObject jsChannel)
    {
        if (Instances.TryGetValue(instanceId, out var connection))
        {
            var channel = new WasmDataChannel(jsChannel);
            connection.RaiseDataChannel(channel);
        }
    }

    [JSExport]
    private static void HandleIceCandidateCallback(int instanceId, JSObject jsCandidate)
    {
        if (Instances.TryGetValue(instanceId, out var connection))
        {
            var candidate = new IceCandidate { native = jsCandidate };
            connection.RaiseIceCandidate(candidate);
        }
    }

    [JSImport("createAnswer", Wasm.JsModule)]
    private static partial Task<JSObject> CreateAnswerAsync(JSObject pc);

    [JSImport("getSdp", Wasm.JsModule)]
    private static partial string GetSdp(JSObject pc, JSObject description);

    [JSImport("setLocalDescription", Wasm.JsModule)]
    private static partial Task SetLocalDescriptionAsync(JSObject pc, JSObject description);

    [JSImport("setRemoteDescription", Wasm.JsModule)]
    private static partial Task SetRemoteDescriptionAsync(JSObject pc, JSObject description);

    [JSImport("addIceCandidate", Wasm.JsModule)]
    private static partial Task AddIceCandidateAsync(JSObject pc, JSObject candidate);
    
    public void Dispose()
    {
        Instances.Remove(id);
        native.Dispose();
    }
    
    internal override async Task<SessionDescriptionInit> createAnswer()
    {
        var jsInit = await CreateAnswerAsync(native);
        var sdp = GetSdp(native, jsInit);
        return new SessionDescriptionInit { native = jsInit, sdp = sdp };
    }
    
    internal override Task setLocalDescription(SessionDescriptionInit init)
    {
        var jsInit = (JSObject)init.native;
        return SetLocalDescriptionAsync(native, jsInit);
    }
        
    internal override async Task<int> setRemoteDescription(SessionDescriptionInit init)
    {
        var jsInit = (JSObject)init.native;
        await SetRemoteDescriptionAsync(native, jsInit);
        return 0;
    }
    
    internal override async Task addIceCandidate(IceCandidateInit init)
    {
        var candidateInit = (JSObject)init.native!;
        await AddIceCandidateAsync(native, candidateInit);
    }
}