// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.JSInterop;


// ReSharper disable CheckNamespace
namespace Friflo.SmGui.WebRTC;


[SupportedOSPlatform("browser")]
internal sealed partial class WasmPeerConnection : PeerConnection, IDisposable
{
    private readonly    JSObject                                    native;
    private readonly    IJSObjectReference                          jsModule;
    private readonly    DotNetObjectReference<WasmPeerConnection>   dotNetRef;
    
    public override     string?                                     ToString() => native.ToString();
    
    internal WasmPeerConnection(IJSObjectReference jsModule) 
    {
        this.jsModule   = jsModule;
        dotNetRef       = DotNetObjectReference.Create(this);
        
        // Instantiate native browser RTCPeerConnection directly inside the constructor
        native          = CreatePeerConnection();
        
        // Register event listeners on the native JS side using the shared module
        jsModule.InvokeVoidAsync("registerPeerConnectionCallbacks", native, dotNetRef).AsTask().GetAwaiter().GetResult();
    }

    [JSImport("createPeerConnection", Wasm.JsModule)]
    private static partial JSObject CreatePeerConnection();

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

    [JSInvokable]
    public void HandleDataChannel(JSObject jsChannel) 
    {
        // Pass the shared module down to the data channel
        var channel = new WasmDataChannel(jsChannel, jsModule);
        RaiseDataChannel(channel);
    }

    [JSInvokable]
    public void HandleIceCandidate(object jsCandidate) 
    {
        var candidate = new IceCandidate { native = jsCandidate };
        RaiseIceCandidate(candidate);
    }
    
    public void Dispose()
    {
        dotNetRef.Dispose();
        native.Dispose();
    }
    
    internal override SessionDescriptionInit createAnswer()
    {
        var jsInit = CreateAnswerAsync(native).GetAwaiter().GetResult();
        var sdp = GetSdp(native, jsInit);
        return new SessionDescriptionInit { native = jsInit, sdp = sdp };
    }
    
    internal override Task setLocalDescription(SessionDescriptionInit init)
    {
        var jsInit = (JSObject)init.native;
        return SetLocalDescriptionAsync(native, jsInit);
    }
        
    internal override int setRemoteDescription(SessionDescriptionInit init)
    {
        var jsInit = (JSObject)init.native;
        SetRemoteDescriptionAsync(native, jsInit).GetAwaiter().GetResult();
        return 0;
    }
    
    internal override void addIceCandidate(IceCandidateInit init)
    {
        var candidateInit = (JSObject)init.native!;
        AddIceCandidateAsync(native, candidateInit).GetAwaiter().GetResult();
    }
}