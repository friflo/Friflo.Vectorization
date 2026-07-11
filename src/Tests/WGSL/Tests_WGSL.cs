using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Friflo.Vectorization.WebGPU;
using Friflo.WGSL.Transpiler.CodeFixes;
using Friflo.WGSL.Transpiler.CSharp;
using NUnit.Framework;

// ReSharper disable UseCollectionExpression
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
    
    public static (CsMethod, ImmutableArray<WgslFile>) GetShaders(Type type, [CallerMemberName] string callerName = "")
    {
        var methodInfo = type.GetMethod(callerName);
        if (methodInfo == null) throw new InvalidOperationException("Could not find method " + callerName);
        
        var files           = new List<WgslFile>();
        var shaders         = new List<CsShader>();
        var attributesData  = methodInfo.GetCustomAttributesData();
        
        foreach (var data in attributesData)
        {
            if (data.AttributeType != typeof(ShaderAttribute)) continue;
            var args = data.ConstructorArguments;
            var path = (string)args[0].Value;
            if (!path!.StartsWith("~/")) throw new InvalidOperationException("expect path starts with ~/ - path:" + path);
            
            path = path.Substring(2);
            var resourceName = "Tests." + path.Replace('/', '.'); 
            var wgsl = ReadWgslResource(resourceName);
            files.Add(new WgslFile { NormalizedPath = path, Content = wgsl, Hash = 0 });
            shaders.Add(new CsShader {
                path = path,
                frag = args[1].Value as string,
                vert = args[2].Value as string,
            });
        }
        var method = new CsMethod {
            Name            = "",
            Hash            = "",
            DeclaringType   = default,
            Parameters      = default,
            DrawVertexIndex = null,
            Modifier        = default,
            Shaders         = shaders.ToValueArray(),
            TypeInfos       = default
        };
        return (method, files.ToImmutableArray());
    }
    
    
    [Test]
    [Shader("~/shaders/triangle.wgsl")]
    public static void Tests_WGSL_Parse_triangle()
    {
        var (_, files) = GetShaders(typeof(Tests_WGSL));
        var metadata = WgslSuperpowerParser.ParseShader(files[0].Content);
        
        Assert.AreEqual(4, metadata.Structs.Count);
        Assert.AreEqual(2, metadata.EntryPoints.Count);
        Assert.AreEqual(2, metadata.Bindings.Count);
    }
    
    [Test]
    [Shader("~/shaders/raymarcher_no_texture.wgsl")]
    public static void Tests_WGSL_Parse_raymarcher_no_texture()
    {
        var (_, files) = GetShaders(typeof(Tests_WGSL));
        var metadata = WgslSuperpowerParser.ParseShader(files[0].Content);
        
        Assert.AreEqual(4, metadata.Structs.Count);
        Assert.AreEqual(2, metadata.EntryPoints.Count);
        Assert.AreEqual(1, metadata.Bindings.Count);
    }
    
    [Test]
	[Shader("~/shaders/triangle.wgsl", vertex: "vs_main", fragment: "fs_main")]
    public static void Tests_WGSL_GenerateParameters()
    {
        var (method, files) = GetShaders(typeof(Tests_WGSL));
        var result = CodeFixer.CreateShaderParams(method, files);
        
        Assert.That(result.Errors.Length, Is.EqualTo(0));
        Assert.That(result.Parameters, Is.EqualTo(
            """
            (RenderPass pass, RenderConfig config,
                    [BindStorage(0, 0)]         InBuffer<TriangleStorage> mesh_data,
                    [BindUniform(1, 0)]         in MyUniforms myUniforms)
            """));
    }
    
    [Test]
	[Shader("~/shaders/shadowMapping/vertex.wgsl",    vertex:   "main")]
	[Shader("~/shaders/shadowMapping/fragment.wgsl",  fragment: "main")]
    public static void Tests_WGSL_GenerateSamplerTextureView()
    {
        var (method, files) = GetShaders(typeof(Tests_WGSL));
        var result = CodeFixer.CreateShaderParams(method, files);
        
        Assert.That(result.Errors.Length, Is.EqualTo(0));
        Assert.That(result.Parameters, Is.EqualTo(
            """
            (RenderPass pass, RenderConfig config,
                    [BindUniform(0, 0)]         in Scene scene,
                    [BindUniform(1, 0)]         in Model model,
                    [texture_depth_2d(0, 1)]    GpuTextureView shadowMap,
                    [SamplerComparison(0, 2)]    GpuSampler shadowSampler)
            """));
    }
    
    
    [Test]
	[Shader("~/shaders/basic.vert.wgsl",                  vertex:   "main")]
	[Shader("~/shaders/sampleTextureMixColor.frag.wgsl",  fragment: "main")]
    public static void Tests_WGSL_Generate_texture_2d()
    {
        var (method, files) = GetShaders(typeof(Tests_WGSL));
        var result = CodeFixer.CreateShaderParams(method, files);
        
        Assert.That(result.Errors.Length, Is.EqualTo(0));
        Assert.That(result.Parameters, Is.EqualTo(
            """
            (RenderPass pass, RenderConfig config,
                    [BindUniform(0, 0)]         in Uniforms uniforms,
                    [SamplerFiltering(0, 1)]    GpuSampler mySampler,
                    [texture_2d(0, 2, ST.f32)]    GpuTextureView myTexture)
            """));
    }
    
}