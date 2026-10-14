using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Tasks;

// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Http;


internal record struct SignalMessage(string type, string? app, string? connection, string? sdpOrCandidate)
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
    internal readonly   SignalClient    client;

    public   override   string          ToString() => name;

    internal TmGuiApp(SignalClient client, string name) {
        this.name   = name;
        this.client = client;
    }
}



/// <summary> Single <see cref="TmSignaling"/> instance is shared by all sessions. </summary>
public sealed class TmSignaling
{
    public   readonly   ConcurrentDictionary<string, TmGuiApp>      guiApps = [];
    private  readonly   ConcurrentDictionary<string, SignalClient>  clients = [];

    
    public TmSignaling(TmGuiBackend backend)
    {
    }
    
    internal async Task ProcessMessageAsync(SignalClient client, ReadOnlyMemory<byte> buffer)
    {
        if (!SignalMessage.TryParse(buffer.Span, out var message)) {
            return;
        }

        switch (message.type)
        {
            case "register":
                if (!string.IsNullOrEmpty(message.app)) {
                    Console.WriteLine($"Register app: '{message.app}'");
                    guiApps[message.app] = new TmGuiApp(client, message.app);
                }
                break;
                
            case "connect":
                if (!string.IsNullOrEmpty(message.app)) {
                    var foundApp = guiApps.TryGetValue(message.app, out var guiApp);
                    if (foundApp) {
                        client.guiApp               = guiApp;
                        clients[client.connection]  = client;
                    }
                    var result = foundApp ? "Found" : "NotFound";
                    var msg = new SignalMessage("ConnectResponse", message.app, client.connection, result);
                    var errorResponse = JsonSerializer.SerializeToUtf8Bytes(msg);
                    await client.SendAsync(errorResponse);
                    Console.WriteLine($"Connect app: '{message.app}'  {result}");
                }
                break;

            case "offer":
            case "answer":
            case "candidate":
                var sender          = client;
                // Case 1: Sender is a Client forwarding an offer or candidate to the App
                if (sender.guiApp != null && sender != sender.guiApp.client)
                {
                    var routeMessage    = message with { connection =  sender.connection };
                    var routeBuffer     = JsonSerializer.SerializeToUtf8Bytes(routeMessage);
                    await sender.guiApp.client.SendAsync(routeBuffer);
                }
                // Case 2: Sender is an App forwarding an answer or candidate back to the Client
                else
                {
                    if (message.connection != null) {
                        if (clients.TryGetValue(message.connection, out var connectedClient)) {
                            await connectedClient.SendAsync(buffer);
                        }
                    }
                }
                break;
        }
    }
}