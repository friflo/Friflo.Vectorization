// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using Friflo.GPU;


// ReSharper disable once CheckNamespace
namespace Friflo.ImGui;

internal sealed class DrawModule : IGpuDeviceModule
{

    internal readonly   Font            defaultFont;
    internal readonly   GuiModule       guiModule;
    
    
    internal DrawModule(ImGuiBackend backend)
    {
        defaultFont = backend.CreateDefaultFont();

        guiModule = new GuiModule();
    }
    
    public void Dispose()
    {
        guiModule.Dispose();
        defaultFont.DisposeInternal();
    }
}
