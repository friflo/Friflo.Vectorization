// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using Friflo.SmGui.Runtime;
using SDL3;

// ReSharper disable CheckNamespace
namespace Friflo.WGPU.SDL3;

public sealed class Sdl3EventHandler : SmRuntimeEventHandler
{
    public   static readonly    Sdl3EventHandler    Instance = new();
    internal static readonly    uint                NotifyEventReadyType = SDL.RegisterEvents(1);
    
    private Sdl3EventHandler() { }
    
    protected override void OnNotifyEventReady()
    {
        var ev = new SDL.Event {
            Type = NotifyEventReadyType,
            User = new SDL.UserEvent { Type = NotifyEventReadyType, Timestamp = SDL.GetTicksNS(), Code = 0 }
        };
        SDL.PushEvent(ref ev);
    }
}