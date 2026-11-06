# friflo SmGui WebRTC


## Description

The goal of Friflo.SmGui.WebRTC is to act as a **real** serverless alternative to an ASP.NET HTTP server.  
In this case GUI vector data is now streamed via WebRTC data channels instead via an HTTP WebSocket.

The key difference of a **WebRTC** setup:  
An app built with this setup **is not a server** anymore.  
A server requires hosting and configuration: endpoint, IP, port, firewall, router, ... .  
The downside of a server setup in detail: Hosting a server forces to pull a credit card.  
Operating a server on a smartphone or data center node may be completely forbidden or hard to realize.

## Characteristics

Friflo.SmGui.WebRTC is a pure client architecture:  
The app opens only outgoing connections (a WebSocket for signaling, WebRTC for GUI clients) and never listens on a port.  
It utilizes **WebRTC** and [ClientWebSocket](https://learn.microsoft.com/en-us/dotnet/api/system.net.websockets.clientwebsocket)'s. In a native .NET application [SIPSorcery](https://github.com/sipsorcery-org/sipsorcery) is used for WebRTC.  
In a browser WebRTC is already available as API and used directly. In this case SIPSorcery is not a dependency.
