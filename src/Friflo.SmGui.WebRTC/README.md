# friflo SmGui WebRTC


## Description

The goal of Friflo.SmGui.WebRTC is to act as drop-in replacement for an ASP.NET HTTP server.  
In this case GUI vector data is now streamed via WebRTC data channels instead via an HTTP WebSocket.

The key difference of a **WebRTC** setup:  
An app build with this setup **is not a server** anymore.  
A server requires hosting and configuration: endpoint, IP, port, firewall, router, ... .  
The downside of a server setup in detail: Hosting a server forces to pull a credit card.  
Operating a server on a smartphone or data center node may be completely forbidden or hard to realize.

## Characteristics

Friflo.SmGui.WebRTC is a pure client architecture. It utilizes **WebRTC** and
[ClientWebSocket](https://learn.microsoft.com/en-us/dotnet/api/system.net.websockets.clientwebsocket)'s.  
In a native .NET application [SIPSorcery](https://github.com/sipsorcery-org/sipsorcery) is used for WebRTC.  
In a WASM application WebRTC is already available as browser API and used directly. In this case SIPSorcery is not a dependency anymore.
