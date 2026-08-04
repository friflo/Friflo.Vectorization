using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Friflo.Vectorization.WebGPU;
using Friflo.WGSL.Transpiler.WGSL;
using NUnit.Framework;


// ReSharper disable UseCollectionExpression
// ReSharper disable once InconsistentNaming
namespace Tests.WGSL;

public static class TestWgslUtils
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
    
    public static WgslFile[] GetShaders(Type type, out bool isCompute, [CallerMemberName] string callerName = "")
    {
        isCompute = false;
        var methodInfo = type.GetMethod(callerName);
        if (methodInfo == null) throw new InvalidOperationException("Could not find method " + callerName);
        
        var files           = new List<WgslFile>();
        var attributesData  = methodInfo.GetCustomAttributesData();
        
        foreach (var data in attributesData)
        {
            if (data.AttributeType != typeof(ShaderAttribute)) continue;
            var args = data.ConstructorArguments;
            var path = (string)args[0].Value;
            if (!path!.StartsWith("~/")) throw new InvalidOperationException("expect path starts with ~/ - path:" + path);
            var compute = (string)args[3].Value;
            if (compute != null) isCompute = true;
            
            path = path.Substring(2);
            var resourceName = "Tests." + path.Replace('/', '.'); 
            var wgsl    = ReadWgslResource(resourceName);
            files.Add(new WgslFile { NormalizedPath = path, Content = wgsl, Hash = 0, Module = null });
        }
        return files.ToArray();
    }
    
    public static TypeMapping[] LoadTestMappings()
    {
        var mappings = TypeMappings.LoadTypeMappings($"{GetProjectDir()}/{TypeMappings.MappingPath}", out var errors);
        Assert.That(errors.Length, Is.EqualTo(0));
        return mappings;
    }
    
    public static string GetProjectDir()
    {
        return typeof(TestWgslUtils).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "ProjectDir")?.Value;
    }

}