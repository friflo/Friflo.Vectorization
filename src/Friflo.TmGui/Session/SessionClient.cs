// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Friflo.TmGui.Session;

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



public interface IGuiView
{
    void RenderGui(TmBatch batch, int targetWidth, int targetHeight);
}



public sealed class SessionClient
{
    public   readonly   SessionId       sessionId;
    public   readonly   string[]        args;
    public   readonly   TmClient        client;
    public   readonly   TmGuiBackend    backend;
    
    private  readonly   TmSessionLoop   loop;
    private  readonly   TmSession       session;
    
    public              string?         AppId => loop.appId;
    
    internal SessionClient(TmSessionLoop loop, TmSession session, string[] args, TmClient client, TmGuiBackend backend)
    {
        this.loop       = loop;
        this.session    = session;
        this.args       = args;
        this.client     = client;
        this.backend    = backend;
        sessionId       = session.sessionId;
    }
    
    /// <summary>
    /// Triggers <see cref="IGuiView.RenderGui"/> for all clients of the app.
    /// </summary>
    /// <remarks>
    /// Applies to native GUI window, console TUI, browser clients, and terminal emulators.
    /// </remarks>
    public void SetAppDirty()
    {
        loop.SetAppDirty();
    }
    
    /// <summary>
    /// Triggers <see cref="IGuiView.RenderGui"/> for all clients of a specific session.
    /// </summary>
    /// <remarks>
    /// Applies to native GUI window, console TUI, browser clients, and terminal emulators.
    /// </remarks>
    public void SetDirty()
    {
        loop.SetSessionDirty(sessionId);
    }
}

public delegate IGuiView CreateGuiView(SessionClient client);
