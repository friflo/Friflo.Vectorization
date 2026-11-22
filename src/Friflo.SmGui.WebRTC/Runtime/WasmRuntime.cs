// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Friflo.SmGui.RTC.WASM;
using Friflo.SmGui.Session;

// ReSharper disable EmptyConstructor
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.WebRTC;

[SupportedOSPlatform("browser")]
public sealed partial class WasmRuntime : SmRuntime
{
    private static          WasmRuntime?    instance;
    
    public WasmRuntime()
    {
        instance = this;
    }
        
    public static void NotifyEventReady() {
        SetEventReady();
    }
    
    [JSImport("setEventReady", Wasm.JsModule)]
    private static partial void SetEventReady();
    
    [JSExport]
    private static Task ProcessEvents()
    {
        instance?.SessionLoop?.IterateSessions();
        return Task.CompletedTask;
    }
    
    public override SmTimer CreateTimer()
    {
        if (!WasmRtcFactory.IsInitialized) {
            throw new InvalidOperationException(WasmRtcFactory.NotInitializedMsg);
        }
        return new WasmTimer();
    }
}
