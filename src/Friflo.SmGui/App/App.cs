// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Friflo.SmGui.Session;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.SmGui;

/// <summary>
/// Represents the base class for a SmGui application, managing its lifecycle and rendering configuration.<br/>
/// The use of <see cref="SmGuiApp"/> is optional. It establishes a minimal skeleton for graphics-backend agnostic applications.
/// </summary>
/// <remarks>
/// The class is designed to be lightweight and keeps the library thin and non-opinionated.<br/>
/// It provides a lean API so that applications based on <see cref="SmGuiApp"/> remain decoupled from the underlying graphics backend.<br/>
/// Note: This class is used in <c>Friflo.SmGui</c> only by <see cref="AppConfig"/>.
/// </remarks>
public abstract class SmGuiApp : IDisposable
{
    public  Color32 clearValue  = 0x999C9EFF;
    /// <summary>
    /// If initialized by the app the <see cref="SessionLoop"/> is added via <see cref="AppConfig"/> to an HTTP server or a signaling client.
    /// </summary>
    /// <remarks>
    /// <example>E.g. at startup in <c>Program.cs</c>
    /// <code>
    ///     var sessionHandlers = new SessionHandlers();
    ///     
    ///     // nuget:  Friflo.SmGui.Http
    ///     var webApp = HttpUtils.CreateWebApplication(new IPEndPoint(IPAddress.IPv6Any, 8080));
    ///     webApp.UseWebSocketGuiSessions(sessionHandlers);
    ///     await webApp.StartAsync();
    ///     
    ///     // nuget:  Friflo.SmGui.WebRTC
    ///     SmSignalingClient.Connect(sessionHandlers, new Uri("wss://localhost:8081/signaling"), "test-app", "auto");
    /// </code>
    /// </example>
    /// </remarks>
    public  SmSessionLoop? SessionLoop { get; protected init; }
    
    public abstract void RenderApp(SmBatch batch, int width, int height);
    public virtual  void Dispose() { }
}

/// <summary>
/// Factory delegate used to instantiate a <see cref="SmGuiApp"/> with a given backend.
/// </summary>
public delegate SmGuiApp CreateGuiApp(SmGuiBackend backend);


