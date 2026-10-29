// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Friflo.SmGui.Session;

// ReSharper disable InconsistentNaming
// ReSharper disable ConvertToAutoProperty
// ReSharper disable CheckNamespace
namespace Friflo.SmGui;

/// <summary>
/// UiType defines the supported UI execution modes.<br/>
/// <b>One UI. High Performance. Zero-Alloc. Every Platform. Any Distance.</b><br/>
/// A cross-platform, immediate-mode UI library optimized for GPU-accelerated rendering.<br/>
/// Supports native GUIs and browser-based GUIs streamed as delta vector data.
/// </summary>
/// <remarks>
/// Runs locally or remotely, across desktop, mobile, browser, and terminal environments.<br/>
/// Provides console and remote terminal user interfaces (TUI).<br/>
/// <br/>
/// For remote GUIs or TUIs a <see cref="SmSessionLoop"/> is required.<br/>
/// It processes all client sessions on the main thread, simplifying application development and enabling
/// lock-free access to application and session state.
/// This keeps remote multi-client execution as simple to program as a classic single-threaded application.
/// </remarks>
public enum UiType
{
    None        =  0,
    
    /// <summary>Native App GUI - Vulkan, DX12, Metal, OpenGL (wgpu-native)</summary>
    /// <remarks>Pure native setup for apps or games. Windows, Linux, macOS, Android, iOS. No server / client code included.</remarks>
    NativeGui   =  1,
    /// <summary>Browser GUI (WebSocket) - WebGPU</summary>
    /// <remarks>Requires an SmGui http / WebSocket server streaming the GUI as delta vector data.</remarks>
    WebGui      =  2,
    /// <summary>Browser GUI (WebRTC) - WebGPU</summary>
    /// <remarks>Browser connects via a signaling server to another SmGui WebRTC peer which streams the GUI as vector data.</remarks>
    WebRtcGui   =  3,
    
    /// <summary>Native Console TUI</summary>
    /// <remarks>Pure native. No server / client is used.</remarks>
    ConsoleTui  =  4,
    /// <summary>Terminal Emulator (TCP) TUI</summary>
    /// <remarks>Requires a TCP server streaming the TUI. Same TUI as in console.</remarks>
    TerminalTui =  5,
}

public static class UiTypeExtensions
{
    extension (UiType type) {
        public bool IsGui()     => type is UiType.NativeGui  or UiType.WebGui or UiType.WebRtcGui;
        public bool IsTui()     => type is UiType.ConsoleTui or UiType.TerminalTui;
        public bool IsLocal()   => type is UiType.NativeGui  or UiType.ConsoleTui;
        public bool IsRemote()  => type is UiType.WebGui or UiType.WebRtcGui or UiType.TerminalTui;
    }
}


/// <summary>
/// Represents a GUI view managed by a <see cref="SmSessionLoop"/> and associated with a <see cref="ViewSession"/>, 
/// capable of rendering user interface frames.
/// </summary>
public interface IGuiView
{
    /// <summary>
    /// Renders the GUI into the specified <paramref name="batch"/> for the given viewport dimensions.
    /// </summary>
    void RenderView(SmBatch batch, int targetWidth, int targetHeight);
    
    /// <summary>
    /// Is called (push model) when a message is received for this view, dispatched from <see cref="ViewSession.PostMessage"/>.<br/>
    /// This message handler is always executed on the session loop thread. Typically, the main thread.
    /// </summary>
    /// <remarks>
    /// Can be used to enqueue messages into a local <see cref="Queue{T}"/> for processing inside <see cref="RenderView"/> (pull model).<br/>
    /// Because this method executes on the single-threaded session loop, local queue operations are thread-safe and lock-free.
    /// </remarks>
    void OnMessage(object? state, Payload payload, int data) { }
}


/// <summary>
/// Represents a factory delegate used by <see cref="SmSessionLoop"/> to create a new <see cref="IGuiView"/> instance for a given session.
/// </summary>
/// <param name="session">The <see cref="ViewSession"/> associated with <see cref="IGuiView"/>.</param>
/// <returns>An instance implementing <see cref="IGuiView"/> for the session.</returns>
public delegate IGuiView CreateGuiView(ViewSession session);




/// <summary>
/// Represents an individual client connection managed by a <see cref="SmSessionLoop"/>, 
/// providing access to client-specific state, backend services, and actions.
/// </summary>
/// <remarks>
/// <see cref="ViewSession"/> acts as the execution context for remote views or browser clients.<br/>
/// Note that a native local backend does not utilize a session; it is created and owned directly 
/// by the user rather than through the session loop.
/// </remarks>
public sealed class ViewSession
{
    /// <summary> The internal session id (<c>int</c>) for this session. </summary>
    public readonly SessionId       SessionId;
    
    public readonly string[]        args;

    /// <summary> The UI execution mode for this session. </summary>
    public readonly UiType          UiType;

    public          SmGuiBackend    Backend => backend ?? throw SessionTerminatedException();

    /// <summary> The app id passed to <see cref="SmSessionLoop.appId"/>. </summary>
    public          string?         AppId   => loop.appId;

    /// <summary> Provides access to client actions not related to GUI or rendering. </summary>
    public          ClientAction    Action  => Backend.host.clientAction;

    /// <summary> Enables triggering <see cref="IGuiView.RenderView"/> at configured time intervals. </summary>
    public          LoopTimer       Timer   => loopTimer ?? throw SessionTerminatedException();

#region private fields
    private             SmGuiBackend?   backend;
    private             LoopTimer?      loopTimer;
    // Note! session reference MUST stay private. Otherwise, user may store a reference which prevents GC collecting the session.
    private             SmSession?      session;
    private  readonly   SmSessionLoop   loop;
#endregion
    
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private InvalidOperationException SessionTerminatedException()
    {
        return new InvalidOperationException($"Session has already been terminated. sessionId: {SessionId}");
    }

    internal ViewSession(SmSessionLoop loop, SmSession session, string[] args, SmClient client, SmGuiBackend backend, LoopTimer timer)
    {
        this.loop       = loop;
        this.args       = args;
        this.backend    = backend;
        loopTimer       = timer;
        this.session    = session;
        SessionId       = session.keys.id;
        UiType          = client.UiType;
    }
    
    internal void Reset()
    {
        backend     = null;
        loopTimer   = null;
        session     = null;
    }
    
    /// <summary>
    /// Posts a message via the event loop of <see cref="SmSessionLoop"/> to the <see cref="IGuiView"/> associated with this session.<br/>
    /// The message is dispatched to <see cref="IGuiView.OnMessage"/> on the session loop thread (typically the main thread).<br/>
    /// This method is thread-safe and can be called from any thread.
    /// </summary>
    public void PostMessage(object? state, Payload payload, int data)
    {
        var local = session;
        if (local != null) {
            loop.eventQueue.TryEnqueueEventSync(local.client, SessionId, LoopEventType.PostMessage, state, payload, data);
        }
    }
    
    /// <summary>
    /// Enqueues a redraw (<see cref="IGuiView.RenderView"/>) for all sessions of the app.
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
    /// Enqueues a redraw (<see cref="IGuiView.RenderView"/>) for a specific session.
    /// </summary>
    /// <remarks>
    /// Applies to native GUI window, console TUI, browser clients, and terminal emulators.<br/>
    /// The method is threadsafe and can be called from any thread.
    /// </remarks>
    public void SetDirty()
    {
        if (session != null) {
            loop.SetSessionDirty(session);
        }
    }
}

