using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Friflo.Vectorization.WebGPU;
using Friflo.WGSL.Transpiler.CodeFixes;
using Friflo.WGSL.Transpiler.CSharp;
using NUnit.Framework;


// ReSharper disable UseCollectionExpression
// ReSharper disable once InconsistentNaming
namespace Tests.WGSL;

internal static class WgslUtils
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
    
    public static List<WgslFile> GetShaders(Type type, [CallerMemberName] string callerName = "")
    {
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
            
            path = path.Substring(2);
            var resourceName = "Tests." + path.Replace('/', '.'); 
            var wgsl    = ReadWgslResource(resourceName);
            var module  = WgslParser.ParseShader(wgsl, path);
            files.Add(new WgslFile { NormalizedPath = path, Content = wgsl, Hash = 0, Module = module });
        }
        return files;
    }
}