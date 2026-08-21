// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;

public class GuiModule
{
    internal readonly   GuiInput    input;
    internal readonly   Gui         gui;
    
    internal GuiModule()
    {
        input   = new GuiInput();
        gui     = new Gui(input);
    }
    
    public void NewFrame()              => input.NewFrame();
    public void AddEvent(in ImEvent ev) => input.AddEvent(ev);
    
    internal void Dispose()
    {
        gui.Dispose();
    }
    
}