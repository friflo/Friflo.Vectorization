// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Friflo.Vectorization.Generators;
using Friflo.WGSL.Transpiler.CodeFixes;
using Friflo.WGSL.Transpiler.CSharp;
using Friflo.WGSL.Transpiler.WGSL;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Note: Used small namespace and class name to enable shorter path names in 'Generated' folders

// ReSharper disable InvertIf
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable SuggestVarOrType_Elsewhere
// ReSharper disable CheckNamespace
namespace Friflo;


[Generator]
public sealed class ShaderGen : IIncrementalGenerator
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
            .Select(static (text, ct) => WgslGenerator.CreateWgslFile(text, ct)).Collect();
        
        // --- [Shader]
        var shaderMethod = context.SyntaxProvider.ForAttributeWithMetadataName(
            "Friflo.Vectorization.WebGPU.ShaderAttribute",
            predicate: (node, _) => node is MethodDeclarationSyntax,
            transform: static (ctx, ct) => TransformShader(ctx, ct))
            .Combine(wgslFiles);
        
        var projectDirProvider = context.AnalyzerConfigOptionsProvider
            .Select(static (options, _) => {
                if (options.GlobalOptions.TryGetValue("build_property.MSBuildProjectDirectory", out var projectDir)) {
                    return projectDir;
                }
                return null;
            });

        // Add CompilationProvider does not harm Caching: because it is appended AFTER the heavy 'TransformShader' cache nodes.
        // The expensive syntax transformation remains 100% cached, and the volatile Compilation is only joined at the final emission step.
        context.RegisterSourceOutput(shaderMethod.Combine(projectDirProvider).Combine(context.CompilationProvider), EmitShader);
    }
    
    private static void EmitShader(
        SourceProductionContext spc,
        (((ShaderMethodResult result, ImmutableArray<WgslFile> files), string? projDir), Compilation compilation) source)
    {
        (((ShaderMethodResult result, ImmutableArray<WgslFile> files), string? projDir), Compilation compilation) = source;
        
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
            ulong wgslHash    = 0;

            // files array can be large.
            foreach (var file in files) {
                // method.Shaders array Length typically <= 3. A HashSet<WgslFile> would be worse.
                foreach (var shader in  method.Shaders) {
                    if (file.NormalizedPath.EndsWith(shader.path)) {
                        wgslHash ^= file.Hash;
                    }
                }
            }
            var diags = ShaderValidation.Validate(method, files);
            
            foreach (var diag in diags) {
	            var location = diag.srcLoc.GetFreshLocation(compilation);
                var desc = diag.type == DiagType.Error ? Errors.ShaderValidationError : Errors.ShaderValidationWarning;
	            var diagnostic = Diagnostic.Create(desc, location, diag.message);
	            spc.ReportDiagnostic(diagnostic);
            }
            var generateParameters = method.Parameters.Length == 0  ||
                                    (method.Parameters.Length == 2 && diags.Count > 0);
            AddShaderCodeFixes(spc, compilation, method, files, projDir, generateParameters);
            
            bool hasErrors  = diags.Any(e => e.type == DiagType.Error);
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
        
        var result = ShaderGenerator.CreateShaderMethod(blueprintMethod, hash, diagnostics);
        if (result == null) {
            return new ShaderMethodResult(diagnostics.List);
        }
        return result;
    }
    
    private static void AddShaderCodeFixes(
        SourceProductionContext     spc,
        Compilation                 compilation,
        CsMethod                    method,
        ImmutableArray<WgslFile>    files,
        string?                     projDir,
        bool                        generateParameters)
    {
        var location        = method.MethodLoc.GetFreshLocation(compilation);
        
        var filteredFiles   = CodeFixer.FilterFiles(method, files);
        var properties      = WgslUtils.CreateDictionary(filteredFiles, null, default);
        
        if (generateParameters)
        {
            var diagnostic 	= Diagnostic.Create(Errors.MissingParameters, location, messageArgs: method.Name, properties: properties);
            spc.ReportDiagnostic(diagnostic);

            /* foreach (var error in fixerResult.Errors) {
                diagnostic = Diagnostic.Create(Errors.WgslValidationError, location, messageArgs: error.Message);
                spc.ReportDiagnostic(diagnostic);
            }*/
        } {
            // var diagnostic 	= Diagnostic.Create(Errors.AddShaderTypes, location, messageArgs: method.Name, properties: properties);
            // spc.ReportDiagnostic(diagnostic);
        } {
            var allFiles = WgslUtils.CreateDictionary(files, projDir, default);
            var diagnostic 	= Diagnostic.Create(Errors.GenerateCSharpTypes, location, properties: allFiles);
            spc.ReportDiagnostic(diagnostic);
            
        } 
    }
}

