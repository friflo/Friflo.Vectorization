// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

// #if RTC_BROWSER

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Friflo.SmGui.Session;

// ReSharper disable EmptyConstructor
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.RTC.WASM;


[SupportedOSPlatform("browser")]
public sealed partial class WasmEventReady : SmEventReady
{
    private static volatile bool _isSet;
    
    public WasmEventReady() { }
        
    public override void Set() {
        if (_isSet) {
            return;
        }
        SetEventReady();
    }
    
    [JSImport("setEventReady", Wasm.JsModule)]
    private static partial void SetEventReady();
    
    [JSExport]
    private static void ProcessEvents()
    {
        SessionLoop?.IterateSessions();
        _isSet = false;
    }
} 

[SupportedOSPlatform("browser")]
public sealed partial class WasmTimer : SmTimer, IDisposable
{
    private static readonly Dictionary<int, WasmTimer>  Instances = new();
    private static          int                         _nextId;

    private readonly    int         id;
    private readonly    JSObject    native;
    
    private WasmTimer()
    {
        id = ++_nextId;
        Instances[id] = this;

        native = CreateTimer(id);
    }

    public override void Dispose()
    {
        Instances.Remove(id);
        native.Dispose();
    }
    
    public static SmTimer CreateTimer()
    {
        if (!WasmRtcFactory.IsInitialized) {
            throw new InvalidOperationException(WasmRtcFactory.NotInitializedMsg);
        }
        return new WasmTimer();
    }
    
    protected override void StartTimer()
    {
        StartTimer(native);
    }
    
    protected override void StopTimer()
    {
        StopTimer(native);
    }
    
    protected override void SetTimerPeriod(int period)
    {
    }
    
    [JSImport("createTimer", Wasm.JsModule)]
    private static partial JSObject CreateTimer(int instanceId);
    
    [JSImport("startTimer", Wasm.JsModule)]
    private static partial void StartTimer(JSObject timer);
    
    [JSImport("startTimer", Wasm.JsModule)]
    private static partial void StopTimer(JSObject timer);
    
    [JSExport]
    private static void HandleTimerCallback(int instanceId)
    {
        if (Instances.TryGetValue(instanceId, out var worker))
        {
            worker.RaiseTick();
        }
    }
}

// #endif
