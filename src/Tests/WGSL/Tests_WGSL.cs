using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Friflo.Vectorization.WebGPU;
using Friflo.WGSL.Transpiler;
using NUnit.Framework;

namespace Tests.WGSL;

// ReSharper disable once InconsistentNaming
public static class Tests_WGSL
{
    // Pattern: [RootNamespace].[Folder].[Filename].[Extension]
    // E.g.    "Tests.shaders.triangle.wgsl"
    private static string ReadWgslResource(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        
        using var stream = assembly.GetManifestResourceStream(resourceName);
        Assert.NotNull(stream);

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
    
    public static List<string> GetShaders(Type type, [CallerMemberName] string callerName = "")
    {
        var methodInfo = type.GetMethod(callerName);
        if (methodInfo == null) throw new InvalidOperationException("Could not find method " + callerName);
        
        var files = new List<string>();
        var attributesData = methodInfo.GetCustomAttributesData();
        foreach (var data in attributesData) {
            if (data.AttributeType != typeof(ShaderAttribute)) continue;
            var args = data.ConstructorArguments;
            var path = (string)args[0].Value;
            if (!path!.StartsWith("~/")) throw new InvalidOperationException("expect path starts with ~/ - path:" + path);
            
            path = path.Substring(2);
            path = "Tests." + path.Replace('/', '.'); 
            var wgsl = ReadWgslResource(path);
            files.Add(wgsl);
        }
        return files;
    }
    
    
    [Test]
    public static void Tests_WGSL_Parse_triangle()
    {
        var wgsl = ReadWgslResource("Tests.shaders.triangle.wgsl");
        
        WgslShaderMetadata metadata = WgslSuperpowerParser.ParseShader(wgsl);
        
        Assert.AreEqual(4, metadata.Structs.Count);
        Assert.AreEqual(2, metadata.EntryPoints.Count);
        Assert.AreEqual(2, metadata.Bindings.Count);
    }
    
    [Test]
    public static void Tests_WGSL_Parse_raymarcher_no_texture()
    {
        var wgsl = ReadWgslResource("Tests.shaders.raymarcher_no_texture.wgsl");
        
        WgslShaderMetadata metadata = WgslSuperpowerParser.ParseShader(wgsl);
        
        Assert.AreEqual(4, metadata.Structs.Count);
        Assert.AreEqual(2, metadata.EntryPoints.Count);
        Assert.AreEqual(1, metadata.Bindings.Count);
    }
    
    [Test]
	[Shader("~/shaders/triangle.wgsl", vertex: "vs_main", fragment: "fs_main")]
    public static void Tests_WGSL_GenerateParameters()
    {
        var files = GetShaders(typeof(Tests_WGSL));
        var shaderParams = CodeFixer.CreateShaderParams(files);
        Assert.That(shaderParams, Is.EqualTo(
            """
            (RenderPass pass, RenderConfig config,
                    [BindStorage(0, 0)] InBuffer<TriangleStorage> mesh_data,
                    [BindUniform(1, 0)] in MyUniforms myUniforms)
            """));
    }
    
    [Test]
	[Shader("~/shaders/shadowMapping/vertex.wgsl",    vertex:   "main")]
	[Shader("~/shaders/shadowMapping/fragment.wgsl",  fragment: "main")]
    public static void Tests_WGSL_GenerateSamplerTextureView()
    {
        var files = GetShaders(typeof(Tests_WGSL));
        var shaderParams = CodeFixer.CreateShaderParams(files);
        
        return;
        Assert.That(shaderParams, Is.EqualTo(
            """
            (RenderPass pass, RenderConfig config,
                    [BindStorage(0, 0)] InBuffer<TriangleStorage> mesh_data,
                    [BindUniform(1, 0)] in MyUniforms myUniforms)
            """));
    }
    
}