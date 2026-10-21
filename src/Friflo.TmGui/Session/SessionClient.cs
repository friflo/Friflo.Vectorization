// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Friflo.TmGui.Session;

// ReSharper disable ConvertToAutoProperty
// ReSharper disable CheckNamespace
namespace Friflo.TmGui;


public readonly struct SessionId : IEquatable<SessionId>
{
    public   readonly   Guid    value;
    internal readonly   string? str;

    public   override   string  ToString() => str ?? "none";
    
    internal SessionId(long id)
    {
        value   = Guid.Parse($"{id:D32}");
        str     = id.ToString();
    }
   
    private SessionId(Guid guid) {
        value   = guid;
        str     = value.ToString();
    }
    
    internal static SessionId FromSpan(ReadOnlySpan<char> sidSpan)
    {
        if (long.TryParse(sidSpan, out var sidInt)) {
            return new SessionId(sidInt);
        }
        if (Guid.TryParse(sidSpan, out Guid guid)) {
            return new SessionId(guid);
        }
        return default;
    }
    
    public override bool Equals(object? obj)        => obj is SessionId other && Equals(other);
    public override int  GetHashCode()              => value.GetHashCode();
    public          bool Equals(SessionId other)    => value == other.value;
    
    public static   bool operator ==(SessionId left, SessionId right)   => left.value == right.value;
    public static   bool operator !=(SessionId left, SessionId right)   => left.value != right.value;
}



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



public sealed class SessionClient
{
    public   readonly   SessionId       sessionId;
    public   readonly   string[]        args;
    public   readonly   TmClient        client;
    public   readonly   TmGuiBackend    backend;
    
    private  readonly   TmSessionLoop   loop;
    private  readonly   LoopTimer       loopTimer;
    
    public              string?         AppId       => loop.appId;
    public              IClientAction   Action      => backend.host.clientAction;
    public              LoopTimer       Timer       => loopTimer;
    public   override   string          ToString()  => $"sessionId: {sessionId}";

    internal SessionClient(TmSessionLoop loop, TmSession session, string[] args, TmClient client, TmGuiBackend backend, LoopTimer timer)
    {
        this.loop       = loop;
        this.args       = args;
        this.client     = client;
        this.backend    = backend;
        loopTimer       = timer;
        sessionId       = session.sessionId;
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
        loop.SetSessionDirty(sessionId);
    }
}

/// <summary>
/// Represents a factory delegate used by <see cref="TmSessionLoop"/> to create a new <see cref="IGuiView"/> instance for a given client session.
/// </summary>
/// <param name="client">The client associated with the session.</param>
/// <returns>An instance implementing <see cref="IGuiView"/> for the client.</returns>
public delegate IGuiView CreateGuiView(SessionClient client);
