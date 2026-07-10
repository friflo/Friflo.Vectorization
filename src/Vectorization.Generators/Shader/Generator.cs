// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Friflo.Vectorization.Generators;
using Friflo.WGSL.Transpiler;
using Friflo.WGSL.Transpiler.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Note: Used small namespace and class name to enable shorter path names in 'Generated' folders

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable SuggestVarOrType_Elsewhere
// ReSharper disable CheckNamespace
namespace Friflo;


public readonly struct WgslFile
{
    public required string  NormalizedPath  { get; init; }
    public required ulong   Hash            { get; init; }
    public required string  Content         { get; init; }
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
        var wgslFiles = context.AdditionalTextsProvider
        .Where(file => file.Path.EndsWith(".wgsl", StringComparison.OrdinalIgnoreCase))
        .Select((text, cancellationToken) =>
        {
            var content             = text.GetText(cancellationToken)?.ToString() ?? string.Empty;
            return new WgslFile {
                NormalizedPath  = text.Path.Replace("\\", "/"),
                Hash            = ComputeFnv1A64(content),
                Content         = content
            };
        }).Collect();
        
        // --- [Shader]
        var shaderMethod = context.SyntaxProvider.ForAttributeWithMetadataName(
            "Friflo.Vectorization.WebGPU.ShaderAttribute",
            predicate: (node, _) => node is MethodDeclarationSyntax,
            transform: TransformShader)
            .Combine(wgslFiles);

        // Add CompilationProvider does not harm Caching: because it is appended AFTER the heavy 'TransformShader' cache nodes.
        // The expensive syntax transformation remains 100% cached, and the volatile Compilation is only joined at the final emission step.
        context.RegisterSourceOutput(shaderMethod.        Combine(context.CompilationProvider), EmitShader);
    }
    
    private static void EmitShader(
        SourceProductionContext spc,
        ((ShaderMethodResult Result, ImmutableArray<WgslFile> Files), Compilation Compilation) source)
    {
        ((ShaderMethodResult result, ImmutableArray<WgslFile> files), Compilation compilation) = source;
        
        if (result.error.exceptionMessage != null) {
            result.error.ReportException(spc);
            return;
        }
        foreach (var diagnostic in result.diagnostics) {
            diagnostic.ReportDiagnostic(spc);
        }
        var method      = result.method;
        if (method == null) return;
        
        // spc.AddSource(emissionResult.name, emissionResult.code);  // test without WGSL hash replacement

        var shaders = method.Shaders;
        ulong wgslHash = 0;

        var wgslContents = new List<string>();
        
        foreach (var file in files) {
            foreach (var shader in shaders)
            {
                if (file.NormalizedPath.EndsWith(shader.path)) {
                    wgslHash   ^= file.Hash;
                    wgslContents.Add(file.Content);
                }
            }
        }
        if (method.Parameters.Length == 0) {
            AddShaderParameterDiagnostic(spc, compilation, result, wgslContents);
        }
        var emitShader  = new ShaderEmitter(method);
        var code        = emitShader.Emit();

        var finalSourceCode = code.Replace("__WGSL_HASH_PLACEHOLDER__", $"0x{wgslHash:x}UL");

        spc.AddSource(result.fileName!, finalSourceCode);
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
    
    private static ShaderMethodResult TransformShader(GeneratorAttributeSyntaxContext ctx, CancellationToken _)
    {
        Location? methodLocation = null;
        try {
            var targetSymbol = ctx.TargetSymbol;
            methodLocation = targetSymbol.Locations.FirstOrDefault();
            return GenerateShader(ctx.SemanticModel, targetSymbol);
        } catch (Exception exception) {
            var exceptionMessage = $"{exception.GetType()} : {exception.Message}";
            var error = new GeneratorError(exceptionMessage, exceptionMessage, methodLocation);
            return new ShaderMethodResult(error);
        }
    }
    
    private static ShaderMethodResult GenerateShader(SemanticModel semanticModel, ISymbol targetSymbol)
    {
        if (targetSymbol is not IMethodSymbol blueprintMethod) {
            return new ShaderMethodResult([]);
        }
        var diagnostics = new Diagnostics { BlueprintMethod = blueprintMethod };
        var attributes  = blueprintMethod.GetAttributes();
        
        var hash = "";
        // var methodSignature = methodSymbol.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);
        // var hash = "_" + GeneratorUtils.GetMd5Hash(methodSignature).Substring(0, 4); // 8 chars is usually enough
        
        var result = CreateShaderMethod(attributes, blueprintMethod, hash, diagnostics);
        if (result == null) {
            return new ShaderMethodResult(diagnostics.List);
        }
        return result;
    }
    
    private static void AddShaderParameterDiagnostic(SourceProductionContext spc, Compilation compilation, ShaderMethodResult result, List<string> wgslContents)
    {
        var parameters = CodeFixer.CreateShaderParams(wgslContents);
        var properties = ImmutableDictionary<string, string?>.Empty
            .Add($"ShaderParams", parameters);
            
        var location 	= result.GetFreshLocation(compilation);
        var diagnostic 	= Diagnostic.Create(Errors.MissingParameters, location, messageArgs: result.method!.Name, properties: properties);
        spc.ReportDiagnostic(diagnostic);
    }
}

