// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

// #if RTC_BROWSER

using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable UnusedMethodReturnValue.Local
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.RTC.WASM;


[SupportedOSPlatform("browser")]
internal sealed partial class WasmDataChannel : DataChannel, IDisposable
{
    private static readonly ConcurrentDictionary<int, WasmDataChannel>  Instances = new();
    private static          int                                         _nextId;

    private readonly    int                 id;
    private readonly    JSObject            native;
    private             DataChannelState    cachedState;
        
    internal override   DataChannelState    readyState => cachedState;
    
    internal override   bool                ordered => GetOrdered(native);

    internal WasmDataChannel(string label, JSObject native) : base(label) 
    {
        id = Interlocked.Increment(ref _nextId);
        Instances[id] = this;

        this.native = native;
        
        // Fetch the initial state once during construction to handle pre-opened channels correctly
        var stateStr = GetState(native);
        cachedState = Enum.TryParse<DataChannelState>(stateStr, true, out var state) ? state : DataChannelState.connecting;
        
        // Register event listeners on the native JS side passing the instance ID
        RegisterDataChannelCallbacks(id, native);
    }

    [JSImport("getState", Wasm.JsModule)]
    private static partial string GetState(JSObject channel);

    [JSImport("getOrdered", Wasm.JsModule)]
    private static partial bool GetOrdered(JSObject channel);

    [JSImport("registerDataChannelCallbacks", Wasm.JsModule)]
    private static partial void RegisterDataChannelCallbacks(int instanceId, JSObject channel);

    [JSImport("sendString", Wasm.JsModule)]
    private static partial bool SendString(JSObject channel, string message);

    [JSImport("sendBinary", Wasm.JsModule)]
//  private static partial bool SendBinary(JSObject channel, IntPtr ptr,  int offset, int length); // does not provide an advantage in context of allocation.
    private static partial bool SendBinary(JSObject channel, byte[] data, int offset, int length);

    // --- callback handler - must return a Task ---
    [JSExport]
    private static Task HandleStateChangeCallback(int instanceId, string stateStr)
    {
        if (Instances.TryGetValue(instanceId, out var channel))
        {
            // Update cached state dynamically on lifecycle events
            if (Enum.TryParse<DataChannelState>(stateStr, true, out var state)) {
                channel.cachedState = state;
            }
            if (state == DataChannelState.open) {
                channel.RaiseOpen();
            }
        }
        return Task.CompletedTask;
    }

    [JSExport]
    private static Task HandleMessageCallback(int instanceId, int protocol, byte[] data)
    {
        if (Instances.TryGetValue(instanceId, out var channel))
        {
            // TODO: Optimize incoming data marshalling to avoid heap allocations per message 
            // (e.g., by utilizing shared memory buffers or zero-copy memory views if available).
            channel.RaiseMessage((DataChannelPayloadProtocols)protocol, data);
        }
        return Task.CompletedTask;
    }

    [JSExport]
    private static Task HandleCloseCallback(int instanceId)
    {
        if (Instances.TryGetValue(instanceId, out var channel))
        {
            channel.cachedState = DataChannelState.closed;
            channel.RaiseClose();
        }
        return Task.CompletedTask;
    }
    
    public void Dispose()
    {
        Instances.TryRemove(id, out _);
        native.Dispose();
    }
    
    internal override void send(string message)
    {
        SendString(native, message);
    }
    
    internal override void send(byte[] data, int offset, int length)
    {
        /* fixed(byte* ptr = data) {
            SendBinary(native, (nint)ptr, offset, length);
        } */
        SendBinary(native, data, offset, length);
    }
}

// #endif
