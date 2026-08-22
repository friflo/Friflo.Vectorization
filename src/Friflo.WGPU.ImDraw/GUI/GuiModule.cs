// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;

public sealed class GuiModule
{
    public   readonly   GuiInput    input;
    internal readonly   GuiHost     host;
    
    internal GuiModule()
    {
        input   = new GuiInput();
        host    = new GuiHost(input);
    }
    
    public void NewFrame()              => input.NewFrame();
    public void AddEvent(in ImEvent ev) => input.AddEvent(ev);
    
    internal void Dispose()
    {
        host.Dispose();
    }
    
}