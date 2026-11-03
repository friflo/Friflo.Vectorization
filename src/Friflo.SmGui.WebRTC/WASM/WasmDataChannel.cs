// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Microsoft.JSInterop;

// ReSharper disable CheckNamespace
namespace Friflo.SmGui.WebRTC;


internal sealed class WasmDataChannel : DataChannel, IDisposable
{
    private readonly    IJSObjectReference                      native;
    private readonly    DotNetObjectReference<WasmDataChannel>  dotNetRef;
        
    internal override DataChannelState readyState 
    {
        get 
        {
            var stateStr = native.InvokeAsync<string>("getState").GetAwaiter().GetResult();
            return Enum.TryParse<DataChannelState>(stateStr, true, out var state) ? state : DataChannelState.closed;
        }
    }
    
    internal override bool ordered 
    {
        get => native.InvokeAsync<bool>("getOrdered").GetAwaiter().GetResult();
        set => native.InvokeVoidAsync("setOrdered", value).AsTask().GetAwaiter().GetResult();
    }
    
    internal override string label 
    {
        get => native.InvokeAsync<string>("getLabel").GetAwaiter().GetResult();
        set => native.InvokeVoidAsync("setLabel", value).AsTask().GetAwaiter().GetResult();
    }

    public override string? ToString() => native.ToString();

    internal WasmDataChannel(IJSObjectReference native, IJSObjectReference jsModule) 
    {
        this.native = native;
        dotNetRef = DotNetObjectReference.Create(this);
        
        // Register event listeners on the native JS side using the shared module
        jsModule.InvokeVoidAsync("registerDataChannelCallbacks", native, dotNetRef).AsTask().GetAwaiter().GetResult();
    }
    
    [JSInvokable]
    public void HandleMessage(int protocol, byte[] data)
    {
        RaiseMessage((DataChannelPayloadProtocols)protocol, data);
    }

    [JSInvokable]
    public void HandleClose()
    {
        RaiseClose();
    }
    
    public void Dispose()
    {
        dotNetRef.Dispose();
        native.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
    
    internal override void send(string message)
    {
        native.InvokeVoidAsync("send", message).AsTask().GetAwaiter().GetResult();
    }
    
    internal override void send(byte[] data, int offset, int length)
    {
        var buffer = (offset == 0 && length == data.Length) ? data : data.AsSpan(offset, length).ToArray();
        native.InvokeVoidAsync("sendBinary", buffer).AsTask().GetAwaiter().GetResult();
    }
}