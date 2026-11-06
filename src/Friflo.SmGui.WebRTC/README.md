# friflo SmGui WebRTC


## Description

The goal of Friflo.SmGui.WebRTC is to act as a **real** serverless alternative to an ASP.NET HTTP server.  
GUI vector data is streamed via WebRTC data channels instead of an HTTP WebSocket.

The key difference of a **WebRTC** setup:  
An app built with this setup **does not act as a server**.  
A server requires hosting and configuration: endpoint, IP, port, firewall, router, ... .  
Hosting a server usually means paying for infrastructure.  
Operating a server on a smartphone or data center node may be forbidden or hard to realize.

## Characteristics

Friflo.SmGui.WebRTC is a pure client architecture:  
The app opens only outgoing connections (a WebSocket for signaling, WebRTC for GUI clients) and never listens on a port.  
It utilizes **WebRTC** and [ClientWebSocket](https://learn.microsoft.com/en-us/dotnet/api/system.net.websockets.clientwebsocket).  
In a native .NET application [SIPSorcery](https://github.com/sipsorcery-org/sipsorcery) is used for WebRTC.  
In a browser WebRTC is already available as an API and used directly. In this case SIPSorcery is not a dependency.
