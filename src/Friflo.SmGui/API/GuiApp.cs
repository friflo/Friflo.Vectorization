// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Friflo.SmGui.Session;

// ReSharper disable CheckNamespace
namespace Friflo.SmGui;

/// <summary>
/// Represents the base class for a SmGui application, managing its lifecycle and rendering configuration.<br/>
/// The use of <see cref="SmGuiApp"/> is optional. It establishes a minimal skeleton for graphics-backend agnostic applications.
/// </summary>
/// <remarks>
/// The class is designed to be lightweight and keeps the library thin and non-opinionated.<br/>
/// It provides a lean API so that applications based on <see cref="SmGuiApp"/> remain decoupled from the underlying graphics backend.<br/>
/// Note: <c>Friflo.SmGui</c> itself does not depend on or utilize this class.
/// </remarks>
public abstract class SmGuiApp : IDisposable
{
    public          Color32         clearValue = 0x999C9EFF;
    public          SmSessionLoop?  Loop { get; protected init; }
    
    public abstract void RenderApp(SmBatch batch, int width, int height);
    public virtual  void Dispose() { }
}

/// <summary>
/// Factory delegate used to instantiate a <see cref="SmGuiApp"/> with a given backend.
/// </summary>
public delegate SmGuiApp CreateGuiApp(SmGuiBackend backend);
