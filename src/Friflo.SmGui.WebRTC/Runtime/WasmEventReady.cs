// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

// #if RTC_BROWSER

using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Friflo.SmGui.RTC.WASM;
using Friflo.SmGui.Session;

// ReSharper disable EmptyConstructor
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.WebRTC;


[SupportedOSPlatform("browser")]
public sealed partial class WasmEventReady : SmEventReady
{
    private static volatile bool _isSet;
    
    public WasmEventReady() { }
        
    public override void Set() {
        if (_isSet) {
            return;
        }
        _isSet = true;
        SetEventReady();
    }
    
    [JSImport("setEventReady", Wasm.JsModule)]
    private static partial void SetEventReady();
    
    [JSExport]
    private static Task ProcessEvents()
    {
        _isSet = false;
        SessionLoop?.IterateSessions();
        return Task.CompletedTask;
    }
}

// #endif
