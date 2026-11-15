// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;

// ReSharper disable CheckNamespace
namespace Friflo.SmGui;


public abstract class GuiApp : IDisposable
{
    public abstract void RenderApp(SmBatch batch,  int width, int height);
    public abstract void Dispose();
}

public delegate GuiApp CreateGuiApp(SmGuiBackend backend);
