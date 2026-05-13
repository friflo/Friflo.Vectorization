// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Microsoft.CodeAnalysis;
using System.Text;

// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.Generators;

public static class GeneratorUtils
{
    public static void AppendRefKind(StringBuilder sb, RefKind refKind)
    {
        switch (refKind) {
            case RefKind.Ref:
                sb.Append("ref ");
                break;
            case RefKind.In:
                sb.Append("in ");
                break;
            case RefKind.Out:
                sb.Append("out ");
                break;
        }
    }
    
    public static string GetGenericTypeArguments(INamedTypeSymbol symbol)
    {
        var sb = new StringBuilder();
        foreach (var arg in symbol.TypeArguments) {
            if (sb.Length > 0) {
                sb.Append(", ");
            }
            var name = arg.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            sb.Append(name);
        }
        return sb.ToString();
    }

    public static bool HasAttribute(ImmutableArray<AttributeData> attributes, string attributeName)
    {
        attributeName = "global::" + attributeName;
        foreach (var attributeData in attributes) {
            if (attributeData.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == attributeName) {
                return true;
            }
        }
        return false;
    }
    
    public static  AttributeData? GetAttributeData(ImmutableArray<AttributeData> attributes, string attributeName)
    {
        attributeName = "global::" + attributeName;
        foreach (var attributeData in attributes) {
            if (attributeData.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == attributeName) {
                return attributeData;
            }
        }
        return null;
    }
    
    public static string GetMd5Hash(string input)
    {
        using (MD5 md5 = MD5.Create())
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(input);
            byte[] hashBytes = md5.ComputeHash(inputBytes);
            return BitConverter.ToString(hashBytes).Replace("-", "");
        }
    }

    public static void AddSource(IncrementalGeneratorPostInitializationContext ctx, string fileName)
    {
        string originalCode = GetContent($"Friflo.Vectorization.Generators.Files.{fileName}");
        var sourcePath = $"Friflo.Vectorization.Intrinsics/{fileName}";
        string newCode = originalCode.Replace("Generators.Static", "Friflo.Vectorization.Intrinsics");
        ctx.AddSource(sourcePath, newCode);
    }
    
    private static string GetContent(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        
        // This attempts to find the resource by checking if the name ends with your filename
        // This is safer than hardcoding the full namespace path.
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

        if (resourceName == null)
        {
            var available = string.Join(", ", assembly.GetManifestResourceNames());
            throw new Exception($"Resource '{fileName}' not found. Available resources: {available}");
        }

        using Stream stream = assembly.GetManifestResourceStream(resourceName);
        using StreamReader reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

}