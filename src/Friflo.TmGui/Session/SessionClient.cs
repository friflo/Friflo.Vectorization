// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using Friflo.TmGui.Session;

// ReSharper disable InconsistentNaming
// ReSharper disable ConvertToAutoProperty
// ReSharper disable CheckNamespace
namespace Friflo.TmGui;


/// <summary>
/// Represents a GUI view managed by a <see cref="TmSessionLoop"/>, capable of rendering user interface frames.
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
/// <see cref="SessionClient"/> acts as the execution context for remote views or browser clients.<br/>
/// Note that a native local backend does not utilize a client session; it is created and owned directly 
/// by the user rather than through the session loop.
/// </remarks>
public sealed class SessionClient
{
    public   readonly   SessionId       SessionId;
    public   readonly   string[]        args;
    
    private  readonly   TmClient        client;
    private  readonly   TmGuiBackend    backend;
    private  readonly   TmSessionLoop   loop;
    private  readonly   LoopTimer       loopTimer;
    
    public              TmGuiBackend    Backend     => backend;
    public              TmClient        Client      => client;
    public              string?         AppId       => loop.appId;
    public              IClientAction   Action      => Backend.host.clientAction;
    public              LoopTimer       Timer       => loopTimer;
    public   override   string          ToString()  => $"sessionId: {SessionId}";

    internal SessionClient(TmSessionLoop loop, TmSession session, string[] args, TmClient client, TmGuiBackend backend, LoopTimer timer)
    {
        // NOTE! Must not store a reference to a heavy session instance. User may store a reference which prevents GC collecting the session.
        this.loop       = loop;
        this.args       = args;
        this.client     = client;
        this.backend    = backend;
        loopTimer       = timer;
        SessionId       = session.keys.id;
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
    /// Enqueues a redraw (<see cref="IGuiView.RenderGui"/>) for all clients of a specific session.
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
/// Represents a factory delegate used by <see cref="TmSessionLoop"/> to create a new <see cref="IGuiView"/> instance for a given client session.
/// </summary>
/// <param name="client">The client associated with the session.</param>
/// <returns>An instance implementing <see cref="IGuiView"/> for the client.</returns>
public delegate IGuiView CreateGuiView(SessionClient client);
