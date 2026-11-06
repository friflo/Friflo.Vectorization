# friflo SmGui WebRTC


## Goal
Programs need a UI. A UI turns a simple goal into a complex problem. SmGui removes that problem.  
Friflo.SmGui.WebRTC takes it one step further: the UI can be shared with any browser via a single link, without hosting the app anywhere.

## Description

The goal of Friflo.SmGui.WebRTC is to act as a **real** serverless alternative to an ASP.NET HTTP server.  
GUI vector data is streamed via WebRTC data channels instead of an HTTP WebSocket.  

Friflo.SmGui.WebRTC supports multiple concurrent sessions. Each session has its own state and its own UI,
so many browser clients can use the same application at the same time, independently of each other.

The key difference of a **WebRTC** setup:  
An app built with this setup **does not act as a server**.  
A server requires hosting and configuration: endpoint, IP, port, firewall, router, ... .  
Hosting a server usually means paying for infrastructure.  
Operating a server on a smartphone or data center node may be forbidden or hard to realize.

## Characteristics

Friflo.SmGui.WebRTC is a pure client architecture:  
The app opens only outgoing connections (a WebSocket for signaling, WebRTC for GUI clients).  
Unlike most "serverless" offerings, the app itself requires no hosting and never listens on a port.  
It utilizes **WebRTC** and [ClientWebSocket](https://learn.microsoft.com/en-us/dotnet/api/system.net.websockets.clientwebsocket).  
In a native .NET application [SIPSorcery](https://github.com/sipsorcery-org/sipsorcery) provides WebRTC.  
In a browser the built-in WebRTC API is used directly, so SIPSorcery is not a dependency.
