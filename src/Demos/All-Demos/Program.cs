// See https://aka.ms/new-console-template for more information

using System;
using System.Net;
using Friflo.SmGui;
using Friflo.SmGui.Http;
using Friflo.SmGui.WebRTC;
using TestConsole;

var config = new AppConfig();

// WebSocket browser sessions: this app hosts the page.  nuget: Friflo.SmGui.Http
var webApp = HttpUtils.CreateWebApplication(new IPEndPoint(IPAddress.IPv6Any, 8080));
webApp.UseWebSocketGuiSessions(config, new Uri("https://localhost:8080"));
await webApp.StartAsync();

// WebRTC browser sessions: signaling server hosts the page.  nuget: Friflo.SmGui.WebRTC
SmSignalingClient.Connect(config, new Uri("https://localhost:8081/?app=test-app"), AppIdType.Auto);

MonoGameRunner.Run("BasicGui",  1280, 1200, config, backend => new Demos.BasicGui.BasicGuiApp(backend));
MonoGameRunner.Run("BasicDraw", 1280, 1200, config, backend => new Demos.BasicDraw.BasicDrawApp(backend));

WgpuRunner.Run("BasicGui",      1280, 1200, config, backend => new Demos.BasicGui.BasicGuiApp(backend));
WgpuRunner.Run("BasicDraw",     1280, 1200, config, backend => new Demos.BasicDraw.BasicDrawApp(backend));

KniRunner.Run("BasicGui",       1280, 1200, config, backend => new Demos.BasicGui.BasicGuiApp(backend));
KniRunner.Run("BasicDraw",      1280, 1200, config, backend => new Demos.BasicDraw.BasicDrawApp(backend));

RaylibRunner.Run("BasicGui",    1280, 1200, config, backend => new Demos.BasicGui.BasicGuiApp(backend));
RaylibRunner.Run("BasicDraw",   1280, 1200, config, backend => new Demos.BasicDraw.BasicDrawApp(backend));

OpenTkRunner.Run("BasicGui",    1280, 1200, config, backend => new Demos.BasicGui.BasicGuiApp(backend));
OpenTkRunner.Run("BasicDraw",   1280, 1200, config, backend => new Demos.BasicDraw.BasicDrawApp(backend));

await webApp.StopAsync();

AppRunner.Run("Particles",      1280, 1200, wgpu => new Shaders.Particles.Renderer(wgpu));
AppRunner.Run("RenderTest",     1280, 1200, wgpu => new Shaders.RenderTest.Renderer(wgpu));
AppRunner.Run("ShadowMapping",  1280, 1200, wgpu => new Shaders.ShadowMapping.Renderer(wgpu));
AppRunner.Run("InstancedCube",  1280, 1200, wgpu => new Shaders.InstancedCube.Renderer(wgpu));
AppRunner.Run("TwoCubes",       1280, 1200, wgpu => new Shaders.TwoCubes.Renderer(wgpu));
AppRunner.Run("TexturedCube",   1280, 1200, wgpu => new Shaders.TexturedCube.Renderer(wgpu));
AppRunner.Run("ConfigTest",     1280, 1200, wgpu => new Shaders.RenderTest.ConfigTest(wgpu));

