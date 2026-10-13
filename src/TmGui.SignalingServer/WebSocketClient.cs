// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Net.WebSockets;


// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
// ReSharper disable CheckNamespace
namespace TmGui.SignalingServer;



internal class WebSocketSignalingClient
{
    private             WebSocket       webSocket;
    

    internal WebSocketSignalingClient(WebSocket webSocket)
    {
        this.webSocket = webSocket;
    }


    internal static async Task HandleClientSessionAsync(WebSocketSignalingClient client, Signaling signaling, CancellationToken cancellationToken)
    {

    }
}