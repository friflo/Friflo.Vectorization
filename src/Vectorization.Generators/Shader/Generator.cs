// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Threading;
using Friflo.Vectorization.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;


// Note: Used small namespace and class name to enable shorter path names in 'Generated' folders

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
        // ------ [Shader] [VertexShader] [FragmentShader]
        var shaderMethod = context.SyntaxProvider.ForAttributeWithMetadataName(
            "Friflo.Vectorization.WebGPU.ShaderAttribute",
            predicate: (node, _) => node is MethodDeclarationSyntax,
            transform: (ctx, ct) => TransformShader(ctx, ct, ShaderTrigger.ShaderAttribute));
        context.RegisterSourceOutput(shaderMethod, Gen.EmitResult);
        
        var vertexShaderMethod = context.SyntaxProvider.ForAttributeWithMetadataName(
            "Friflo.Vectorization.WebGPU.VertexShaderAttribute",
            predicate: (node, _) => node is MethodDeclarationSyntax,
            transform: (ctx, ct) => TransformShader(ctx, ct, ShaderTrigger.VertexShaderAttribute));
        context.RegisterSourceOutput(vertexShaderMethod, Gen.EmitResult);
        
        var fragmentShaderMethod = context.SyntaxProvider.ForAttributeWithMetadataName(
            "Friflo.Vectorization.WebGPU.FragmentShaderAttribute",
            predicate: (node, _) => node is MethodDeclarationSyntax,
            transform: (ctx, ct) => TransformShader(ctx, ct, ShaderTrigger.FragmentShaderAttribute));
        context.RegisterSourceOutput(fragmentShaderMethod, Gen.EmitResult);
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
        
        var code = GenerateShaderMethod(attributes, blueprintMethod, trigger, hash, diagnostics);
        if (code == null) {
            return new EmissionResult("", "", diagnostics.List);
        }
        
        var fileName = Gen.CreateFileName(blueprintMethod, hash);
        return new EmissionResult(fileName, code, diagnostics.List);
    }
}

