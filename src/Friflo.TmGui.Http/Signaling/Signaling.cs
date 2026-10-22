using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Friflo.TmGui.Session;


// ReSharper disable EmptyConstructor
// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Http;


internal record struct SignalMessage(string type, string? app, string? connection, string? sdpOrCandidate, int? sessions = null)
{
    public override string ToString() => $"[{type}]  app: {app}  conn: {connection}";
    
    internal static bool TryParse(ReadOnlySpan<byte> buffer, out SignalMessage message)
    {
        try {
            message = JsonSerializer.Deserialize<SignalMessage>(buffer);
            return true;
        }
        catch (JsonException) {
            message = default;
            return false;
        }
    }
}

public sealed class TmGuiApp
{
    public   readonly   string          name;
    public   readonly   string          url;
    public   readonly   DateTime        registered;
    public              bool            Online          => online;
    public              int             SessionCount    => sessionCount;
    
    internal readonly   TmSignalPeer    peer;
    internal            bool            online;
    internal            int             sessionCount;

    public   override   string          ToString() => name;

    internal TmGuiApp(TmSignalPeer peer, string name, string url) {
        this.name   = name;
        this.peer   = peer;
        this.url    = url;
        online      = true;
        registered  = DateTime.Now;
    }
}


/// <summary> Single <see cref="TmSignaling"/> instance is shared by all sessions. </summary>
public sealed class TmSignaling
{
    public   readonly   ConcurrentDictionary<string, TmGuiApp>      guiApps = [];
    public   readonly   ConcurrentDictionary<string, TmSignalPeer>  clients = [];
    private             int                                         instanceSeq;
    /// <summary>
    /// This action is invoked from arbitrary threads if apps or clients are added or removed. <br/>
    /// If connected to a UI it should call <see cref="TmSessionLoop.SetAppDirty"/>.
    /// </summary>
    public              Action?                                     StateChanged;


    public TmSignaling() { }
    
    internal void SetDirty()
    {
        StateChanged?.Invoke();
    }
    
    internal async Task ProcessMessageAsync(TmSignalPeer peer, ReadOnlyMemory<byte> buffer)
    {
        if (!SignalMessage.TryParse(buffer.Span, out var message)) {
            return;
        }

        switch (message.type)
        {
            // ---------------------------------- called by:  apps ----------------------------------
            case "register":
                if (!string.IsNullOrEmpty(message.app)) {
                    var instance = message.sdpOrCandidate;
                    var app = message.app;
                    if (instance == "auto") {
                        instance = $"{++instanceSeq}";
                    }
                    var appId = instance == null ? app : $"{app}.{instance}";
                    Console.WriteLine($"Register app: '{appId}'");
                    var guiApp = peer.app = new TmGuiApp(peer, appId, $"?app={appId}");
                    guiApps[appId] = guiApp;
                    peer.SetAppSessionCount(message);
                    SetDirty();
                    
                    var msg = new SignalMessage("RegisterResponse", appId, peer.connection, "registered");
                    var errorResponse = JsonSerializer.SerializeToUtf8Bytes(msg);
                    await peer.SendAsync(errorResponse);
                }
                break;

            // ---------------------------------- called by:  clients ----------------------------------
            case "connect":
                if (!string.IsNullOrEmpty(message.app)) {
                    string result;
                    if (guiApps.TryGetValue(message.app, out var guiApp))
                    {
                        peer.connectApp = guiApp;
                        if (guiApp.online) {
                            clients[peer.connection] = peer;
                            SetDirty();
                            result = "found";
                        } else {
                            result = "offline";
                        }
                    } else {
                        result = "not-found";
                    }
                    var msg = new SignalMessage("ConnectResponse", message.app, peer.connection, result);
                    var errorResponse = JsonSerializer.SerializeToUtf8Bytes(msg);
                    await peer.SendAsync(errorResponse);
                    Console.WriteLine($"Connect app: '{message.app}'  {result}");
                }
                break;
            case "close":
                // close Client signaling webSocket. P2P Data Channel was opened
                peer.SetAppSessionCount(message);
                
                if (message.connection != null) {
                    if (clients.TryRemove(message.connection, out var signalPeer)) {
                        SetDirty();
                        await signalPeer.webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
                    }
                }
                break;

            // ---------------------------------- called by:  peers (apps / clients) ----------------------------------
            case "offer":
            case "answer":
            case "candidate":
                var sender = peer;
                // Case 1: Sender is a Client forwarding an offer or candidate to the App
                if (sender.connectApp != null && sender != sender.connectApp.peer)
                {
                    var routeMessage    = message with { connection =  sender.connection };
                    var routeBuffer     = JsonSerializer.SerializeToUtf8Bytes(routeMessage);
                    await sender.connectApp.peer.SendAsync(routeBuffer);
                }
                // Case 2: Sender is an App forwarding an answer or candidate back to the Client
                else
                {
                    peer.SetAppSessionCount(message);
                    
                    if (message.connection != null) {
                        if (clients.TryGetValue(message.connection, out var signalPeer)) {
                            await signalPeer.SendAsync(buffer);
                        }
                    }
                }
                break;
        }
    }
}