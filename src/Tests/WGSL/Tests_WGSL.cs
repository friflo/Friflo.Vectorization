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
    public static void Tests_WGSL_Parse()
    {
        var wgsl = ReadWgslResource("Tests.Shaders.triangle.wgsl");
        
        WgslShaderMetadata metadata = WgslSuperpowerParser.ParseShader(wgsl);
        
        
    }
    
}