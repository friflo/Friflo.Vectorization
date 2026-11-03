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
    private             DataChannelState                        cachedState;
        
    internal override   DataChannelState                        readyState => cachedState;
    
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
        dotNetRef   = DotNetObjectReference.Create(this);
        
        // Fetch the initial state once during construction to handle pre-opened channels correctly
        var stateStr = native.InvokeAsync<string>("getState").GetAwaiter().GetResult();
        cachedState = Enum.TryParse<DataChannelState>(stateStr, true, out var state) ? state : DataChannelState.connecting;
        
        // Register event listeners on the native JS side
        jsModule.InvokeVoidAsync("registerDataChannelCallbacks", native, dotNetRef).AsTask().GetAwaiter().GetResult();
    }
    
    [JSInvokable]
    public void HandleStateChange(string stateStr)
    {
        // Update cached state dynamically on lifecycle events
        if (Enum.TryParse<DataChannelState>(stateStr, true, out var state)) {
            cachedState = state;
        }
    }

    [JSInvokable]
    public void HandleMessage(int protocol, byte[] data)
    {
        RaiseMessage((DataChannelPayloadProtocols)protocol, data);
    }

    [JSInvokable]
    public void HandleClose()
    {
        cachedState = DataChannelState.closed;
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