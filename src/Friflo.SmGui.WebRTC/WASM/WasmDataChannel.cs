// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Microsoft.JSInterop;

// ReSharper disable CheckNamespace
namespace Friflo.SmGui.WebRTC;


[SupportedOSPlatform("browser")]
internal sealed partial class WasmDataChannel : DataChannel, IDisposable
{
    private readonly    JSObject                                    native;
    private readonly    DotNetObjectReference<WasmDataChannel>  dotNetRef;
    private             DataChannelState                        cachedState;
        
    internal override   DataChannelState                        readyState => cachedState;
    
    internal override bool ordered 
    {
        get => GetOrdered(native);
        set => SetOrdered(native, value);
    }
    
    internal override string label 
    {
        get => GetLabel(native);
        set => SetLabel(native, value);
    }

    public override string? ToString() => native.ToString();

    internal WasmDataChannel(JSObject native, IJSObjectReference jsModule) 
    {
        this.native = native;
        dotNetRef   = DotNetObjectReference.Create(this);
        
        // Fetch the initial state once during construction to handle pre-opened channels correctly
        var stateStr = GetState(native);
        cachedState = Enum.TryParse<DataChannelState>(stateStr, true, out var state) ? state : DataChannelState.connecting;
        
        // Register event listeners on the native JS side
        jsModule.InvokeVoidAsync("registerDataChannelCallbacks", native, dotNetRef).AsTask().GetAwaiter().GetResult();
    }

    [JSImport("getState", "socket.js")]
    private static partial string GetState(JSObject channel);

    [JSImport("getOrdered", "socket.js")]
    private static partial bool GetOrdered(JSObject channel);

    [JSImport("setOrdered", "socket.js")]
    private static partial void SetOrdered(JSObject channel, bool value);

    [JSImport("getLabel", "socket.js")]
    private static partial string GetLabel(JSObject channel);

    [JSImport("setLabel", "socket.js")]
    private static partial void SetLabel(JSObject channel, string value);

    [JSImport("sendString", "socket.js")]
    private static partial void SendString(JSObject channel, string message);

    [JSImport("sendBinary", "socket.js")]
    private static partial void SendBinary(JSObject channel, byte[] data, int offset, int length);

    
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
        // TODO: Optimize incoming data marshalling to avoid heap allocations per message 
        // (e.g., by utilizing shared memory buffers or zero-copy memory views if available).
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
        native.Dispose();
    }
    
    internal override void send(string message)
    {
        SendString(native, message);
    }
    
    internal override void send(byte[] data, int offset, int length)
    {
        SendBinary(native, data, offset, length);
    }
}