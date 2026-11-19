// See https://aka.ms/new-console-template for more information

// ReSharper disable ConvertToLocalFunction
using System;
using System.Net;
using Friflo.SmGui;
using Friflo.SmGui.Http;
using Friflo.WGPU;
using Friflo.WGPU.SDL3;
using SDL3;

// HTTP server for Browser sessions.  nuget: Friflo.SmGui.Http
var webApp = HttpUtils.CreateWebApplication(new IPEndPoint(IPAddress.IPv6Any, 8080));
var sessionHandler = webApp.UseWebSocketGuiSessions();
await webApp.StartAsync();

WgpuRunner.Run("SmGui",   1280, 1200, sessionHandler, backend => new Demos.BasicGui.BasicGuiApp(backend));
WgpuRunner.Run("SmDraw",  1280,  720, sessionHandler, backend => new Demos.BasicDraw.BasicDrawApp(backend));

await webApp.StopAsync();

public static class WgpuRunner
{
    public static int Run(string title, int width, int height, WebSessionHandler? sessionHandler, CreateGuiApp createGuiApp)
    {
        var createSessionApp = WebSessionHandler.AddSessionLoop(createGuiApp, sessionHandler);
        if (!SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Gamepad)) throw new Exception($"SDL3 initialization failed: {SDL.GetError()}");
        var window = new Sdl3Window();
        CreateRenderer createRenderer = wgpuHost => new WgpuRenderer(wgpuHost);
        var opt = new InitOptions{ title = title, width = width, height = height, createRenderer = createRenderer, createGuiApp = createSessionApp };
        var result = window.EnterAppMain(opt);
        
        sessionHandler?.RemoveSessionLoop();
        return result;
    }
}