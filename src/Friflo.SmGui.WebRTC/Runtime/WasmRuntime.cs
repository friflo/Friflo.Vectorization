// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Runtime.Versioning;
using Friflo.SmGui.RTC.WASM;
using Friflo.SmGui.Session;


// ReSharper disable CheckNamespace
namespace Friflo.SmGui.WebRTC;

[SupportedOSPlatform("browser")]
public sealed class WasmRuntime : SmRuntime
{
    public override SmTimer CreateTimer()
    {
        if (!WasmRtcFactory.IsInitialized) {
            throw new InvalidOperationException(WasmRtcFactory.NotInitializedMsg);
        }
        return new WasmTimer();
    }
}
