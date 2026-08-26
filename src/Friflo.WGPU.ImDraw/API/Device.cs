// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;
using Friflo.GPU;


// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global
// ReSharper disable once CheckNamespace
namespace Friflo.ImGui;


public static class ImDeviceExtensions      // TODO IM_TEX  remove whole class
{
    extension (GpuDevice device)
    {
        public GuiModule? GetGuiModule()    
        {
            if (device.TryGetModule(out DrawModule module)) {
                return module.guiModule;
            }
            return null;
        }
    }
}