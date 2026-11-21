// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Friflo.SmGui.Session;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.WebRTC;

public enum AppId
{
    /// <summary>
    /// The app id is fixed. Only a single client can be registered with a unique app id.
    /// </summary>
    Fixed,

    /// <summary>
    /// The signaling server assigns a dynamic app id by appending a sequence number to the passed app id (e.g., <c>test-app.4</c>).<br/>
    /// This allows multiple instances of the same application to register on the signaling server.
    /// </summary>
    Auto
}

public sealed partial class SmSignalingClient : ISessionHandler
{
    private SmSessionLoop?      sessionLoop;
    private ClientWebSocket?    clientWebSocket;
        
    private SmSignalingClient(SmSessionLoop? loop)
    {
        sessionLoop = loop;
        loop?.SessionsChanged += SessionsChanged;
    }
    
    // --- ISessionHandler
    public void RemoveSessionLoop() {
        sessionLoop = null;
    }
    
    public void AddSessionLoop(SmSessionLoop loop) {
        sessionLoop = loop;
    }
    
    // ReSharper disable once MemberCanBePrivate.Global
    public static Uri GetSignalingWebSocketUri(Uri uri) {
         return new Uri($"{(uri.Scheme == "https" ? "wss" : "ws")}://{uri.Host}:{uri.Port}/signaling");
    }
    
    public static SmSignalingClient Connect(AppConfig config, Uri uri, string app, AppId appId, CancellationToken cancellationToken = default)
    {
        if (OperatingSystem.IsBrowser()) throw new PlatformNotSupportedException($"Browser requires: {nameof(ConnectAsync)}");
        var wsUri = GetSignalingWebSocketUri(uri);
        var signalingClient = new SmSignalingClient(config.sessionLoop);
        var finalAppId = signalingClient.ConnectAsync(wsUri, app, appId, cancellationToken).GetAwaiter().GetResult(); // stay on main thread
        config.Add(signalingClient);
        config.SetWebRtcUrl(uri, finalAppId);
        
        _ = signalingClient.RunAsync();
        return signalingClient;
    }
    
    public static async Task<SmSessionLoop> ConnectAsync(SmSessionLoop loop, Uri uri, string app, AppId appId, CancellationToken cancellationToken = default)
    {
        var signalingClient =  new SmSignalingClient(loop);
        var wsUri = GetSignalingWebSocketUri(uri);
        await signalingClient.ConnectAsync(wsUri, app, appId, cancellationToken);
        _ = signalingClient.RunAsync();
    
        return loop;
    }
    
    
    private void SessionsChanged(SessionsChangedArgs args)
    {
        var loop = sessionLoop;
        if (loop == null) {
            return;
        }
        var msg     = new SignalMessage{ type = "stats", app = loop.Config.AppId, sessions = loop.SessionCount };
        var payload = JsonSerializer.SerializeToUtf8Bytes(msg, ApiJson.Default.SignalMessage);
        _ =  clientWebSocket?.SendAsync(payload, WebSocketMessageType.Text, true, CancellationToken.None);
    }
        
    public async Task<string?> ConnectAsync(Uri uri, string app, AppId appId, CancellationToken cancellationToken = default)
    {
        clientWebSocket = new ClientWebSocket();

        // Connect to the specified signaling server URL
        await clientWebSocket.ConnectAsync(uri, cancellationToken);
        
        int sessionCount = sessionLoop?.SessionCount ?? 0;
        var appIdType   = appId == AppId.Auto ? "auto" : null;
        var msg         = new SignalMessage{ type = "register", app = app, sdpOrCandidate = appIdType, sessions = sessionCount };
        byte[] payload  = JsonSerializer.SerializeToUtf8Bytes(msg, ApiJson.Default.SignalMessage);

        await clientWebSocket.SendAsync(payload, WebSocketMessageType.Text, true, cancellationToken);
        
        var result = await ReadChunksAsync(clientWebSocket, cancellationToken);
        
        if (result.MessageType != WebSocketMessageType.Text) {
            throw new InvalidOperationException($"expect Text message. was {result.MessageType}");
        }
        var messageBuffer = memoryStream.GetBuffer().AsMemory(0, (int)memoryStream.Length);
        if (!SignalMessage.TryParse(messageBuffer.Span, out var message)) {
            throw new InvalidOperationException($"invalid message response");
        }
        if (message.sdpOrCandidate != "registered") {
            throw new InvalidOperationException($"register failed: {message.sdpOrCandidate}");
        }
        return message.app;
    }
    
    public Task RunAsync()
    {
        if (clientWebSocket == null) throw new InvalidOperationException("ClientWebSocket not initialized");
        // Dedicated background thread execution equivalent to TaskCreationOptions.LongRunning
        var serverTask = Task.Factory.StartNew(
            () => RunLoopAsync(),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default
        ).Unwrap();
        return serverTask;
    }
        
    private async Task RunLoopAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await RunWebSocketReceiveLoopAsync(clientWebSocket!, cancellationToken);
        }
        catch (WebSocketException)
        {
            // Handle unexpected drops or client disconnects gracefully
        }
    }
    
    private readonly    MemoryStream    memoryStream = new();
    private readonly    byte[]          buffer = new byte[1024 * 4];
    
    private async Task RunWebSocketReceiveLoopAsync(WebSocket webSocket, CancellationToken cancellationToken)
    {
        while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var result = await ReadChunksAsync(webSocket, cancellationToken);

            // Process the complete message payload based on its type
            if (result.MessageType == WebSocketMessageType.Text)
            {
                var messageBuffer = memoryStream.GetBuffer().AsMemory(0, (int)memoryStream.Length);
                
                // Handle incoming signaling message (e.g., SDP offer/answer or ICE candidate)
                await ProcessSignalingMessage(webSocket, messageBuffer);
            }
            else if (result.MessageType == WebSocketMessageType.Binary)
            {
                // Handle binary payloads if needed
                // byte[] binaryData = memoryStream.ToArray();
            }
        }
    }
    
    private async Task<WebSocketReceiveResult> ReadChunksAsync(WebSocket webSocket, CancellationToken cancellationToken)
    {
        memoryStream.SetLength(0); // Reset stream for each new message

        WebSocketReceiveResult result;
        // Read chunks until the complete message is received (handles fragmentation)
        do
        {
            result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure, 
                    "Closing by remote peer request", 
                    CancellationToken.None);
                return result;
            }

            memoryStream.Write(buffer, 0, result.Count);
        } 
        while (!result.EndOfMessage);
        
        return result;
    }  
}