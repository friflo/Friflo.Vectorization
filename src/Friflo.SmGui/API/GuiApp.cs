// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;

// ReSharper disable CheckNamespace
namespace Friflo.SmGui;

/// <summary>
/// Represents the base class for a SmGui application, managing its lifecycle and rendering configuration.
/// </summary>
/// <remarks>
/// Designed to be lightweight and graphics-backend agnostic, keeping the library thin and non-opinionated.<br/>
/// It provides a lean API so that applications based on <see cref="SmGuiApp"/> remain decoupled from the underlying graphics backend.<br/>
/// Note: <c>Friflo.SmGui</c> itself does not depend on or utilize this class.
/// </remarks>
public abstract class SmGuiApp : IDisposable
{
    public Color32  clearValue = 0x999C9EFF;
    
    public abstract void RenderApp(SmBatch batch, int width, int height);
    public abstract void Dispose();
}

/// <summary>
/// Factory delegate used to instantiate a <see cref="SmGuiApp"/> with a given backend.
/// </summary>
public delegate SmGuiApp CreateGuiApp(SmGuiBackend backend);
