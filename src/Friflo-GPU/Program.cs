// See https://aka.ms/new-console-template for more information

// ReSharper disable ConvertToLocalFunction
using System;
using System.Net;
using Demos;
using Friflo.SmGui;
using Friflo.SmGui.Http;
using Friflo.WGPU;
using Friflo.WGPU.SDL3;
using SDL3;

var sessionHandler = new SessionHandlers();

// HTTP server for Browser sessions.  nuget: Friflo.SmGui.Http
var webApp = HttpUtils.CreateWebApplication(new IPEndPoint(IPAddress.IPv6Any, DemoConfig.HttpsPort));
webApp.UseWebSocketGuiSessions(sessionHandler);
await webApp.StartAsync();

WgpuRunner.Run("SmGui",   1280, 1200, sessionHandler, backend => new Demos.BasicGui.BasicGuiApp(backend));
WgpuRunner.Run("SmDraw",  1280,  720, sessionHandler, backend => new Demos.BasicDraw.BasicDrawApp(backend));

await webApp.StopAsync();

public static class WgpuRunner
{
    public static int Run(string title, int width, int height, SessionHandlers? sessionHandlers, CreateGuiApp createGuiApp)
    {
        createGuiApp = sessionHandlers?.AddSessionLoop(createGuiApp) ?? createGuiApp;
        if (!SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Gamepad)) throw new Exception($"SDL3 initialization failed: {SDL.GetError()}");
        var window = new Sdl3Window();
        CreateRenderer createRenderer = wgpuHost => new WgpuRenderer(wgpuHost);
        var opt = new InitOptions{ title = title, width = width, height = height, createRenderer = createRenderer, createGuiApp = createGuiApp };
        var result = window.EnterAppMain(opt);
        
        sessionHandlers?.RemoveSessionLoop();
        return result;
    }
}