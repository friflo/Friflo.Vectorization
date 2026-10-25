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
/// UiType goals: One UI. High Performance. Every Platform. Any Distance.<br/>
/// A cross-platform, immediate-mode UI library optimized for GPU-accelerated rendering.<br/>
/// Supports native GUIs and browser-based GUIs streamed as delta vector data.
/// </summary>
/// <remarks>
/// Runs locally or remotely, across desktop, mobile, browser, and terminal environments.<br/>
/// Provides console and remote terminal user interfaces (TUI).<br/>
/// <br/>
/// For remote GUI's or TUI's a <see cref="TmSessionLoop"/> is required.<br/>
/// It renders all client sessions on the main thread, simplifying application development and enabling lock-free access to application and session state.<br/>
/// </remarks>
public enum UiType
{
    None        =  0,
    
    /// <summary>Native App GUI - Vulkan, DX12, Metal, OpenGL (wgpu-native)</summary>
    /// <remarks>Pure native setup for apps or games. Windows, Linux, macOS, Android, iOS. No server / client code included.</remarks>
    NativeGui   =  1,
    /// <summary>Browser GUI (WebSocket) - WebGPU</summary>
    /// <remarks>Requires an TmGui http / WebSocket server streaming the GUI as delta vector data.</remarks>
    WebGui      =  2,
    /// <summary>Browser GUI (WebRTC) - WebGPU</summary>
    /// <remarks>Browser connects via a signaling server to another TmGui WebRTC peer which streams the GUI as vector data.</remarks>
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
/// Represents a GUI view managed by a <see cref="TmSessionLoop"/> and associated with a <see cref="ViewSession"/>, 
/// capable of rendering user interface frames.
/// </summary>
public interface IGuiView
{
    /// <summary>
    /// Renders the GUI into the specified <paramref name="batch"/> for the given viewport dimensions.
    /// </summary>
    void RenderView(TmBatch batch, int targetWidth, int targetHeight);
}


/// <summary>
/// Represents a factory delegate used by <see cref="TmSessionLoop"/> to create a new <see cref="IGuiView"/> instance for a given session.
/// </summary>
/// <param name="session">The <see cref="ViewSession"/> associated with <see cref="IGuiView"/>.</param>
/// <returns>An instance implementing <see cref="IGuiView"/> for the session.</returns>
public delegate IGuiView CreateGuiView(ViewSession session);


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
    public   readonly   UiType          UiType;
    
    public              TmGuiBackend    Backend     => backend      ?? throw SessionTerminatedException();
    public              string?         AppId       => loop.appId;
    public              ClientAction    Action      => Backend.host.clientAction;
    public              LoopTimer       Timer       => loopTimer    ?? throw SessionTerminatedException();

    public   override   string          ToString()  => $"sessionId: {SessionId}  {UiType}";

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
        UiType          = client.UiType;
    }
    
    internal void Reset()
    {
        backend     = null;
        loopTimer   = null;
    }
    
    /// <summary>
    /// Enqueues a redraw (<see cref="IGuiView.RenderView"/>) for all clients of the app.
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
        loop.SetSessionDirty(SessionId);
    }
}

