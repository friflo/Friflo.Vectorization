// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.Generators;


public static class GeneratorUtils
{
    private static readonly SymbolDisplayFormat FullNameFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        // memberOptions: SymbolDisplayMemberOptions.IncludeContainingType,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType // Include this if you want (string x, int y)
    );
    
    internal static string CreateFileName(IMethodSymbol methodSymbol, string hash)
    {
        // path format: <namespace>/<class name>/<method name>
        // this format simplifies navigation in generated files
        var path  = methodSymbol.ContainingType.ToDisplayString();
        var index = path.LastIndexOf('.');
        if (index != -1) {
            path = string.Concat(path.Substring(0, index), "/", path.Substring(index + 1));
        }
        path = $"{path}/{methodSymbol.ToDisplayString(FullNameFormat)}";
        path = path.Replace('<', '{').Replace('>', '}');
        // using hash instead of method signature for file name. Signature would lead to long file names not supported by the OS
        return $"{path}{hash}.g.cs";
    }
    
    internal static void EmitResult(SourceProductionContext productionContext, EmissionResult emissionResult)
    {
        if (emissionResult.error.exceptionMessage != null) {
            emissionResult.error.ReportException(productionContext);
            return;
        }
        foreach (var data in emissionResult.diagnostics) {
            data.ReportDiagnostic(productionContext);
        }
        if (emissionResult.code == "") {
            return;
        }
        var source = emissionResult.code.Replace("\r\n", "\n");
        var text = SourceText.From(source, new UTF8Encoding(false), SourceHashAlgorithm.Sha256);
        productionContext.AddSource(emissionResult.name, text);
    }
    
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
    
    public static List<AttributeData> GetAttributeDatas(ImmutableArray<AttributeData> attributes, string attributeName)
    {
        var result = new List<AttributeData>();
        attributeName = "global::" + attributeName;
        foreach (var attributeData in attributes) {
            if (attributeData.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == attributeName) {
                result.Add(attributeData);
            }
        }
        return result;
    }
    
    public static string GetMd5Hash(string input)
    {
        using var md5       = MD5.Create();
        byte[] inputBytes   = Encoding.UTF8.GetBytes(input);
        byte[] hashBytes    = md5.ComputeHash(inputBytes);
        return BitConverter.ToString(hashBytes).Replace("-", "");
    }

    // Method was called in:
    //      public void Initialize(IncrementalGeneratorInitializationContext context)
    // within:
    //          context.RegisterPostInitializationOutput(ctx => { GeneratorUtils.AddSource(ctx, "AvxVector2.g.cs"); ... }
    public static void AddSource(IncrementalGeneratorPostInitializationContext ctx, string fileName)
    {
        string originalCode = GetContent($"Friflo.Generators.Files.{fileName}");
        var sourcePath = $"Friflo.Vectorization.Intrinsics/{fileName}";
        string newCode = originalCode.Replace("Generators.Static", "Friflo.Vectorization.Intrinsics");
        ctx.AddSource(sourcePath, newCode);
    }
    
    public static void AddSource(SourceProductionContext ctx, string fileName)
    {
        string originalCode = GetContent($"Friflo.Generators.Files.{fileName}");
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