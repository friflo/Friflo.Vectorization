// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Runtime.Versioning;
using Friflo.SmGui.Runtime;

// ReSharper disable CheckNamespace
namespace Friflo.SmGui.WebRTC;

[SupportedOSPlatform("browser")]
public sealed class WasmRuntimeEventHandler : SmRuntimeEventHandler
{
    public static readonly WasmRuntimeEventHandler Instance = new();

    private WasmRuntimeEventHandler() { }
        
    protected override void OnNotifyEventReady()
    {
        WasmRuntime.SetEventReady();
    }
}