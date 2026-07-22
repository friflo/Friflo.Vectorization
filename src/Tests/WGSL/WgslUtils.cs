using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
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
    
    public static WgslFile[] GetShaders(Type type, [CallerMemberName] string callerName = "")
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
            files.Add(new WgslFile { NormalizedPath = path, Content = wgsl, Hash = 0, Module = null });
        }
        return files.ToArray();
    }
    
    public static WgslFile[] LoadAdditionalFilesRecursive(string srcFolder, string baseFolder)
    {
        if (Environment.CurrentDirectory.EndsWith("/linux-x64")) {
            srcFolder = "../" + srcFolder; // use a specific bin folder on GitHub.  See: https://github.com/friflo/Friflo.Vectorization/blob/main/.github/workflows/generators-ci.yml#L55
        }
        var searchPath  = Path.GetFullPath(srcFolder);
        if (!Directory.Exists(searchPath)) {
            throw new InvalidOperationException($"folder not found: searchPath: {searchPath}  CurrentDirectory: {Environment.CurrentDirectory}");
        } 
        var fullBaseDir = searchPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var list = new List<WgslFile>();

        // iterate recursive all *.wgsl files
        foreach (var fullFilePath in Directory.EnumerateFiles(fullBaseDir, "*.wgsl", SearchOption.AllDirectories))
        {
            var relativePath = baseFolder + Path.GetRelativePath(fullBaseDir, fullFilePath);
            var content = File.ReadAllText(fullFilePath);
            list.Add(new WgslFile{ NormalizedPath = relativePath, Content = content, Hash =  0, Module = null });
        }
        return list.ToArray();
    }
}