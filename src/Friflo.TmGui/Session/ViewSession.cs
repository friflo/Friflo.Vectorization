// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;
using Friflo.TmGui.Session;

// ReSharper disable InconsistentNaming
// ReSharper disable ConvertToAutoProperty
// ReSharper disable CheckNamespace
namespace Friflo.TmGui;


/// <summary>
/// Represents a GUI view managed by a <see cref="TmSessionLoop"/> and associated with a <see cref="ViewSession"/>, 
/// capable of rendering user interface frames.
/// </summary>
public interface IGuiView
{
    /// <summary>
    /// Renders the GUI into the specified <paramref name="batch"/> for the given viewport dimensions.
    /// </summary>
    void RenderGui(TmBatch batch, int targetWidth, int targetHeight);
}


/// <summary>
/// Represents an individual client connection managed by a <see cref="TmSessionLoop"/>, 
/// providing access to client-specific state, backend services, and actions.
/// </summary>
/// <remarks>
/// <see cref="ViewSession"/> acts as the execution context for remote views or browser clients.<br/>
/// Note that a native local backend does not utilize a session; it is created and owned directly 
/// by the user rather than through the session loop.
/// </remarks>
public sealed class ViewSession
{
    public   readonly   SessionId       SessionId;
    public   readonly   string[]        args;
    public   readonly   TmClientType    ClientType;
    
    public              TmGuiBackend    Backend     => backend      ?? throw SessionTerminatedException();
    public              string?         AppId       => loop.appId;
    public              ClientAction    Action      => Backend.host.clientAction;
    public              LoopTimer       Timer       => loopTimer    ?? throw SessionTerminatedException();

    public   override   string          ToString()  => $"sessionId: {SessionId}  {ClientType}";

#region private fields
    private             TmGuiBackend?   backend;
    private             LoopTimer?      loopTimer;
    private  readonly   TmSessionLoop   loop;
#endregion
    
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private InvalidOperationException SessionTerminatedException()
    {
        return new InvalidOperationException($"Session has already been terminated. sessionId: {SessionId}");
    }

    internal ViewSession(TmSessionLoop loop, TmSession session, string[] args, TmClient client, TmGuiBackend backend, LoopTimer timer)
    {
        // NOTE! Must not store a reference to a heavy session instance. User may store a reference which prevents GC collecting the session.
        this.loop       = loop;
        this.args       = args;
        this.backend    = backend;
        loopTimer       = timer;
        SessionId       = session.keys.id;
        ClientType      = client.ClientType;
    }
    
    internal void Reset()
    {
        backend     = null;
        loopTimer   = null;
    }
    
    /// <summary>
    /// Enqueues a redraw (<see cref="IGuiView.RenderGui"/>) for all clients of the app.
    /// </summary>
    /// <remarks>
    /// Applies to native GUI window, console TUI, browser clients, and terminal emulators.<br/>
    /// The method is threadsafe and can be called from any thread.
    /// </remarks>
    public void SetAppDirty()
    {
        loop.SetAppDirty();
    }
    
    /// <summary>
    /// Enqueues a redraw (<see cref="IGuiView.RenderGui"/>) for a specific session.
    /// </summary>
    /// <remarks>
    /// Applies to native GUI window, console TUI, browser clients, and terminal emulators.<br/>
    /// The method is threadsafe and can be called from any thread.
    /// </remarks>
    public void SetDirty()
    {
        loop.SetSessionDirty(SessionId);
    }
}

/// <summary>
/// Represents a factory delegate used by <see cref="TmSessionLoop"/> to create a new <see cref="IGuiView"/> instance for a given session.
/// </summary>
/// <param name="session">The <see cref="ViewSession"/> associated with <see cref="IGuiView"/>.</param>
/// <returns>An instance implementing <see cref="IGuiView"/> for the session.</returns>
public delegate IGuiView CreateGuiView(ViewSession session);
