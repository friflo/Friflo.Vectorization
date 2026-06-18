using System.IO;
using System.Reflection;
using Friflo.WGSL.Transpiler;
using NUnit.Framework;

namespace Tests.WGSL;

// ReSharper disable once InconsistentNaming
public static class Tests_WGSL
{
    // Pattern: [RootNamespace].[Folder].[Filename].[Extension]
    // E.g.    "Tests.Shaders.triangle.wgsl"
    private static string ReadWgslResource(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        
        using var stream = assembly.GetManifestResourceStream(resourceName);
        Assert.NotNull(stream);

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
    
    
    [Test]
    public static void Tests_WGSL_Parse_triangle()
    {
        var wgsl = ReadWgslResource("Tests.Shaders.triangle.wgsl");
        
        WgslShaderMetadata metadata = WgslSuperpowerParser.ParseShader(wgsl);
        
        Assert.AreEqual(4, metadata.Structs.Count);
        Assert.AreEqual(2, metadata.EntryPoints.Count);
        Assert.AreEqual(2, metadata.Bindings.Count);
    }
    
    [Test]
    public static void Tests_WGSL_Parse_raymarcher_no_texture()
    {
        var wgsl = ReadWgslResource("Tests.Shaders.raymarcher_no_texture.wgsl");
        
        WgslShaderMetadata metadata = WgslSuperpowerParser.ParseShader(wgsl);
        
        Assert.AreEqual(4, metadata.Structs.Count);
        Assert.AreEqual(2, metadata.EntryPoints.Count);
        Assert.AreEqual(1, metadata.Bindings.Count);
    }
    
}