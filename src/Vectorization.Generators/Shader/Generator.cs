// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Friflo.Vectorization.Generators;
using Friflo.WGSL.Transpiler.CodeFixes;
using Friflo.WGSL.Transpiler.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Note: Used small namespace and class name to enable shorter path names in 'Generated' folders

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable SuggestVarOrType_Elsewhere
// ReSharper disable CheckNamespace
namespace Friflo;


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
            var content = text.GetText(cancellationToken)?.ToString() ?? string.Empty;
            var path    = text.Path.Replace('\\', '/');
            return new WgslFile {
                NormalizedPath  = path,
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
        context.RegisterSourceOutput(shaderMethod.Combine(context.CompilationProvider), EmitShader);
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
        var method = result.method;
        if (method == null) {
            return;
        }
        try {
            ulong   wgslHash    = 0;
            var     foundWgsl   = false;
            
            foreach (var file in files) {
                foreach (var shader in  method.Shaders)
                {
                    if (file.NormalizedPath.EndsWith(shader.path)) {
                        wgslHash ^= file.Hash;
                        foundWgsl = true;
                    }
                }
            }
            var errors = ShaderValidation.Validate(method, files);
            foreach (var error in errors) {
	            var location = error.srcLoc.GetFreshLocation(compilation);
	            var diagnostic = Diagnostic.Create(Errors.ShaderValidationError, location, error.message);
	            spc.ReportDiagnostic(diagnostic);
            }
            AddShaderCodeFixes(spc, compilation, result, files, foundWgsl);
            
            bool hasErrors  = errors.Count > 0;
            var emitShader  = new ShaderEmitter(method);
            var code        = emitShader.Emit(wgslHash, hasErrors);
            spc.AddSource(result.fileName!, code);
        }
        catch (Exception exception)
        {
            var exceptionMessage    = $"{exception.GetType()} : {exception.Message}";
            var methodLocation      = method.MethodLoc.GetFreshLocation(compilation);
            var error               = new GeneratorError(exceptionMessage, exceptionMessage, methodLocation);
            error.ReportException(spc);
        }
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
    
    private static ShaderMethodResult GenerateShader(SemanticModel _, ISymbol targetSymbol)
    {
        if (targetSymbol is not IMethodSymbol blueprintMethod) {
            return new ShaderMethodResult([]);
        }
        var diagnostics = new Diagnostics { BlueprintMethod = blueprintMethod };
        
        var hash = "";
        // var methodSignature = methodSymbol.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);
        // var hash = "_" + GeneratorUtils.GetMd5Hash(methodSignature).Substring(0, 4); // 8 chars is usually enough
        
        var result = CreateShaderMethod(blueprintMethod, hash, diagnostics);
        if (result == null) {
            return new ShaderMethodResult(diagnostics.List);
        }
        return result;
    }
    
    private static void AddShaderCodeFixes(
        SourceProductionContext     spc,
        Compilation                 compilation,
        ShaderMethodResult          result,
        ImmutableArray<WgslFile>    files,
        bool                        generateParameters)
    {
        var location 	= result.method!.MethodLoc.GetFreshLocation(compilation);
        var wgsl        = CodeFixer.CreateWgsl(result.method, files);
        
        if (generateParameters && result.method!.Parameters.Length == 0)
        {
            var properties  = ImmutableDictionary<string, string?>.Empty
                .Add("WGSL", wgsl);
                
            var diagnostic 	= Diagnostic.Create(Errors.MissingParameters, location, messageArgs: result.method!.Name, properties: properties);
            spc.ReportDiagnostic(diagnostic);

            /* foreach (var error in fixerResult.Errors) {
                diagnostic = Diagnostic.Create(Errors.WgslValidationError, location, messageArgs: error.Message);
                spc.ReportDiagnostic(diagnostic);
            }*/
        } {
            var properties  = ImmutableDictionary<string, string?>.Empty
                .Add("WGSL", wgsl);
                
            var diagnostic 	= Diagnostic.Create(Errors.AddShaderTypes, location, messageArgs: result.method!.Name, properties: properties);
            spc.ReportDiagnostic(diagnostic);
        }
    }
}

