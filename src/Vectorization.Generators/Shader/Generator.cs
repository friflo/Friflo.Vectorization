// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Friflo.Vectorization.Generators;
using Friflo.WGSL.Transpiler.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Note: Used small namespace and class name to enable shorter path names in 'Generated' folders

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable SuggestVarOrType_Elsewhere
// ReSharper disable CheckNamespace
namespace Friflo;

public enum ShaderTrigger
{
    ShaderAttribute,
    VertexShaderAttribute,
    FragmentShaderAttribute,
}

[Generator]
public sealed partial class ShaderGen : IIncrementalGenerator
{
    // --- IIncrementalGenerator
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        RegisterStreamingTranspiler(context);
    }
    
    // In algorithmic context this code generator is a "Recursive Descent Streaming Transpiler"
    private static void RegisterStreamingTranspiler(IncrementalGeneratorInitializationContext context)
    {
        var wgslHashes = context.AdditionalTextsProvider
        .Where(file => file.Path.EndsWith(".wgsl", StringComparison.OrdinalIgnoreCase))
        .Select((text, cancellationToken) =>
        {
            var content             = text.GetText(cancellationToken)?.ToString() ?? string.Empty;
            ulong   hash            = ComputeFnv1A64(content);
            var     normalizedPath  = text.Path.Replace("\\", "/");
            return (FilePath: normalizedPath, Hash: hash);
        }).Collect();
        
        // ------ [Shader] [VertexShader] [FragmentShader]
        var shaderMethod = context.SyntaxProvider.ForAttributeWithMetadataName(
            "Friflo.Vectorization.WebGPU.ShaderAttribute",
            predicate: (node, _) => node is MethodDeclarationSyntax,
            transform: (ctx, ct) => TransformShader(ctx, ct, ShaderTrigger.ShaderAttribute))
            .Combine(wgslHashes);
        
        var vertexShaderMethod = context.SyntaxProvider.ForAttributeWithMetadataName(
            "Friflo.Vectorization.WebGPU.VertexShaderAttribute",
            predicate: (node, _) => node is MethodDeclarationSyntax,
            transform: (ctx, ct) => TransformShader(ctx, ct, ShaderTrigger.VertexShaderAttribute))
            .Combine(wgslHashes);

        var fragmentShaderMethod = context.SyntaxProvider.ForAttributeWithMetadataName(
            "Friflo.Vectorization.WebGPU.FragmentShaderAttribute",
            predicate: (node, _) => node is MethodDeclarationSyntax,
            transform: (ctx, ct) => TransformShader(ctx, ct, ShaderTrigger.FragmentShaderAttribute))
            .Combine(wgslHashes);

        // Register outputs individually - zero interference, maximum Roslyn-native caching
        context.RegisterSourceOutput(shaderMethod,         EmitWithHash);
        context.RegisterSourceOutput(vertexShaderMethod,   EmitWithHash);
        context.RegisterSourceOutput(fragmentShaderMethod, EmitWithHash);
    }
    
    private static void EmitWithHash(
        SourceProductionContext spc,
        (EmissionResult EmissionResult, ImmutableArray<(string FilePath, ulong Hash)> Files) source)
    {
        (EmissionResult emissionResult, ImmutableArray<(string FilePath, ulong Hash)> files) = source;
        
        if (string.IsNullOrEmpty(emissionResult.code)) return;
        
        // spc.AddSource(emissionResult.name, emissionResult.code);  // test without WGSL hash replacement

        var targetFile1 = emissionResult.wgslFileName1;
        var targetFile2 = emissionResult.wgslFileName2;
        ulong wgslHash = 0;
        
        foreach (var file in files) {
            var filePath = file.FilePath;
            if (targetFile1 != null && filePath.EndsWith(targetFile1)) {
                wgslHash = file.Hash;
            }
            if (targetFile2 != null && filePath.EndsWith(targetFile2)) {
                wgslHash ^= file.Hash;
            }
        }
        var finalSourceCode = emissionResult.code.Replace("__WGSL_HASH_PLACEHOLDER__", $"0x{wgslHash:x}UL");

        spc.AddSource(emissionResult.name, finalSourceCode);
    }
    
    // High-performance, allocation-free FNV-1a 64-bit string hashing
    private static ulong ComputeFnv1A64(string text)
    {
        ulong hash = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;

        foreach (char c in text)
        {
            hash ^= (byte)c;        hash *= prime;
            hash ^= (byte)(c >> 8); hash *= prime;
        }
        return hash;
    }
    
    private static EmissionResult TransformShader(GeneratorAttributeSyntaxContext ctx, CancellationToken _, ShaderTrigger trigger)
    {
        Location? methodLocation = null;
        try {
            var targetSymbol = ctx.TargetSymbol;
            methodLocation = targetSymbol.Locations.FirstOrDefault();
            return GenerateShader(ctx.SemanticModel, targetSymbol, trigger);
        } catch (Exception exception) {
            var exceptionMessage = $"{exception.GetType()} : {exception.Message}";
            return new EmissionResult(exceptionMessage, exception.StackTrace, methodLocation);
        }
    }
    
    private static EmissionResult GenerateShader(SemanticModel semanticModel, ISymbol targetSymbol, ShaderTrigger trigger)
    {
        if (targetSymbol is not IMethodSymbol blueprintMethod) {
            return new EmissionResult("", "", []);
        }
        var diagnostics = new Diagnostics { BlueprintMethod = blueprintMethod };
        var attributes  = blueprintMethod.GetAttributes();
        
        var hash = "";
        // var methodSignature = methodSymbol.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);
        // var hash = "_" + GeneratorUtils.GetMd5Hash(methodSignature).Substring(0, 4); // 8 chars is usually enough
        
        var result = CreateShaderMethod(attributes, blueprintMethod, trigger, hash, diagnostics);
        if (result == null) {
            return new EmissionResult("", "", diagnostics.List);
        }
        var method = result.method;
        
        var emitShader  = new ShaderEmitter(method, hash);
        var code        = emitShader.Emit(method.Modifier);
        
        var source      = method!.Source;
        var wgslFile1   = source.Shader;
        wgslFile1     ??= source.VertexShader;
        var wgslFile2   = source.FragmentShader;
        
        var fileName = GeneratorUtils.CreateFileName(blueprintMethod, hash);
        return new EmissionResult(fileName, code, diagnostics.List, wgslFile1, wgslFile2);
    }
}

