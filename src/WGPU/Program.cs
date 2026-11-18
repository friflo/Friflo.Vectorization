// See https://aka.ms/new-console-template for more information

// ReSharper disable ConvertToLocalFunction
using System;
using Friflo.SmGui;
using Friflo.WGPU;
using Friflo.WGPU.SDL3;
using SDL3;
using WGPU;


WgpuRunner.Run("SmGui",   1280, 1200, backend => new Demos.BasicGui.BasicGuiApp(backend, true));
WgpuRunner.Run("SmDraw",  1280,  720, backend => new Demos.BasicDraw.BasicDrawApp(backend));


public static class WgpuRunner
{
    public static int Run(string title, int width, int height, CreateGuiApp createGuiApp)
    {
        if (!SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Gamepad)) throw new Exception($"SDL3 initialization failed: {SDL.GetError()}");
        var window = new Sdl3Window();
        CreateRenderer createRenderer = wgpuHost => new WgpuRenderer(wgpuHost);
        var opt = new InitOptions{ title = title, width = width, height = height, createRenderer = createRenderer, createGuiApp = createGuiApp };
        return window.EnterAppMain(opt);
    }
}