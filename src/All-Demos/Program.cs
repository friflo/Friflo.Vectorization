// See https://aka.ms/new-console-template for more information

using TestConsole;

MonoGameRunner.Run("SmGui", 1280, 1200, backend => new Demos.BasicDraw.BasicDrawApp(backend, true));
MonoGameRunner.Run("SmDraw",1280,  720, backend => new Demos.BasicGui.BasicGuiApp(backend));

WgpuRunner.Run("SmGui",   1280, 1200, backend => new Demos.BasicDraw.BasicDrawApp(backend, true));
WgpuRunner.Run("SmDraw",  1280,  720, backend => new Demos.BasicGui.BasicGuiApp(backend));

AppRunner.Run("Particles",      1280, 720, wgpu => new Shaders.Particles.Renderer(wgpu));
AppRunner.Run("RenderTest",     1280, 720, wgpu => new Shaders.RenderTest.Renderer(wgpu));
AppRunner.Run("ShadowMapping",  1280, 720, wgpu => new Shaders.ShadowMapping.Renderer(wgpu));
AppRunner.Run("InstancedCube",  1280, 720, wgpu => new Shaders.InstancedCube.Renderer(wgpu));
AppRunner.Run("TwoCubes",       1280, 720, wgpu => new Shaders.TwoCubes.Renderer(wgpu));
AppRunner.Run("TexturedCube",   1280, 720, wgpu => new Shaders.TexturedCube.Renderer(wgpu));
AppRunner.Run("ConfigTest",     1280, 720, wgpu => new Shaders.RenderTest.ConfigTest(wgpu));

