// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;

// ReSharper disable InvertIf
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU.Runtime;


public static class WgpuResource
{
     
    public static ReadOnlySpan<byte> GetResource(Type type, string resourcePath)
    {
        return GetResource(type.Assembly, resourcePath);
    }
     
    private static unsafe ReadOnlySpan<byte> GetResource(Assembly assembly, string resourcePath)
    {
#if DEBUG
        var res = GetResourceFromFile(assembly, resourcePath);
        if (res.Length != 0) {
            return res;
        }
#endif
        var assemblyName = assembly.GetName().Name;
        var resourceName = $"{assemblyName}.{resourcePath.Replace('/', '.')}";
        
        var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null) { 
            throw new FileNotFoundException($"Resource '{resourceName}' not found");
        }
        if (stream is UnmanagedMemoryStream unmanagedStream)
        {
            var span = new ReadOnlySpan<byte>(unmanagedStream.PositionPointer, (int)unmanagedStream.Length);
        
            // Detect UTF-8 BOM and skip
            if (span.Length >= 3 && span[0] == 0xEF && span[1] == 0xBB && span[2] == 0xBF) {
                return span.Slice(3);
            }
            return span;
        }
        throw new InvalidOperationException($"Resource '{resourceName}' not found");
    }
    
    private static ReadOnlySpan<byte> GetResourceFromFile(Assembly assembly, string resourceName)
    {
        // var cleanPath       = resourceName.Replace(assembly.GetName().Name + ".", "");
        // int lastDot         = cleanPath.LastIndexOf('.');
        // var relativePath    = cleanPath.Substring(0, lastDot).Replace('.', '/') + cleanPath.Substring(lastDot);
        var fullPath = Path.Combine(AppContext.BaseDirectory, "../../../", resourceName);

        if (File.Exists(fullPath))
        {
            var bytes = File.ReadAllBytes(fullPath);
            return (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) 
                ? bytes.AsSpan(3) : bytes;
        }
        return default;
    }
}

