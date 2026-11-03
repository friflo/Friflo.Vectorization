// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Microsoft.JSInterop;


// ReSharper disable CheckNamespace
namespace Friflo.SmGui.WebRTC;


internal sealed class WasmPeerConnection : PeerConnection, IDisposable
{
    private readonly    IJSObjectReference                          native;
    private readonly    IJSObjectReference                          jsModule;
    private readonly    DotNetObjectReference<WasmPeerConnection>   dotNetRef;
    
    public override     string?                                     ToString() => native.ToString();
    
    internal WasmPeerConnection(IJSRuntime jsRuntime, IJSObjectReference jsModule) 
    {
        this.jsModule   = jsModule;
        dotNetRef       = DotNetObjectReference.Create(this);
        
        // Instantiate native browser RTCPeerConnection directly inside the constructor
        native = jsRuntime.InvokeAsync<IJSObjectReference>("eval", "new RTCPeerConnection()").GetAwaiter().GetResult();
        
        // Register event listeners on the native JS side using the shared module
        jsModule.InvokeVoidAsync("registerPeerConnectionCallbacks", native, dotNetRef).AsTask().GetAwaiter().GetResult();
    }

    [JSInvokable]
    public void HandleDataChannel(IJSObjectReference jsChannel) 
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
        native.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
    
    internal override SessionDescriptionInit createAnswer()
    {
        var jsInit = native.InvokeAsync<IJSObjectReference>("createAnswer").GetAwaiter().GetResult();
        var sdp = native.InvokeAsync<string>("getSdp", jsInit).GetAwaiter().GetResult();
        return new SessionDescriptionInit { native = jsInit, sdp = sdp };
    }
    
    internal override Task setLocalDescription(SessionDescriptionInit init)
    {
        var jsInit = (IJSObjectReference)init.native;
        return native.InvokeVoidAsync("setLocalDescription", jsInit).AsTask();
    }
        
    internal override int setRemoteDescription(SessionDescriptionInit init)
    {
        var jsInit = (IJSObjectReference)init.native;
        native.InvokeVoidAsync("setRemoteDescription", jsInit).AsTask().GetAwaiter().GetResult();
        return 0;
    }
    
    internal override void addIceCandidate(IceCandidateInit init)
    {
        var candidateInit = init.native;
        native.InvokeVoidAsync("addIceCandidate", candidateInit).AsTask().GetAwaiter().GetResult();
    }
}