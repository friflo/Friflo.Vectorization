// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Friflo.Vectorization.Generators;
using Friflo.Vectorization.Generators.AVX;
using Friflo.Vectorization.Generators.WGSL;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;


// ReSharper disable UseCollectionExpression
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable once CheckNamespace
// Note: Used small namespace and class name to enable shorter path names in 'Generated' folders
namespace Friflo;

[Generator]
public sealed partial class Gen : IIncrementalGenerator
{
    // --- IIncrementalGenerator
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        RegisterStreamingTranspiler(context);
        // RegisterTranspiler_BadCommonApproach(context);
        
        /* context.RegisterPostInitializationOutput(ctx => {
            GeneratorUtils.AddSource(ctx, "AvxVector2.g.cs");
            GeneratorUtils.AddSource(ctx, "AvxVector3.g.cs");
            GeneratorUtils.AddSource(ctx, "AvxVector4.g.cs");
            GeneratorUtils.AddSource(ctx, "MathUtils.g.cs");
            GeneratorUtils.AddSource(ctx, "VectorUtils.g.cs");
        }); */
    }
    
    // In algorithmic context this code generator is a "Recursive Descent Streaming Transpiler"
    private static void RegisterStreamingTranspiler(IncrementalGeneratorInitializationContext context)
    {
        // Filter for methods with the attribute
        var queryMethod = context.SyntaxProvider.ForAttributeWithMetadataName(
            "Friflo.Engine.ECS.QueryAttribute",
            predicate: (node, _) => node is MethodDeclarationSyntax,
            transform: (ctx, ct) => TransformAttribute(ctx, ct, GenerateTrigger.QueryAttribute));
        context.RegisterSourceOutput(queryMethod, GeneratorUtils.EmitResult);
        
        var vectorizeMethod = context.SyntaxProvider.ForAttributeWithMetadataName(
            "Friflo.Vectorization.VectorizeAttribute",
            predicate: (node, _) => node is MethodDeclarationSyntax,
            transform: (ctx, ct) => TransformAttribute(ctx, ct, GenerateTrigger.VectorizeAttribute));
        context.RegisterSourceOutput(vectorizeMethod, GeneratorUtils.EmitResult);
        
        var hasVectorize = vectorizeMethod.Collect().Select((list, _) => !list.IsEmpty);
        context.RegisterSourceOutput(hasVectorize, (spc, found) => {
            if (!found) {
                return;
            }
            GeneratorUtils.AddSource(spc, "AvxVector2.g.cs");
            GeneratorUtils.AddSource(spc, "AvxVector3.g.cs");
            GeneratorUtils.AddSource(spc, "AvxVector4.g.cs");
            GeneratorUtils.AddSource(spc, "MathUtils.g.cs");
            GeneratorUtils.AddSource(spc, "VectorUtils.g.cs");
        });
        
        var kernelMethod = context.SyntaxProvider.ForAttributeWithMetadataName(
            "Friflo.Vectorization.GPU.KernelAttribute",
            predicate: (node, _) => node is MethodDeclarationSyntax,
            transform: (ctx, ct) => TransformAttribute(ctx, ct, GenerateTrigger.KernelAttribute));
        context.RegisterSourceOutput(kernelMethod, GeneratorUtils.EmitResult);
    }
    
    // ReSharper disable once UnusedMember.Local
    private static void RegisterTranspiler_BadCommonApproach(IncrementalGeneratorInitializationContext context)
    {
        var methodDeclarations = context.SyntaxProvider.ForAttributeWithMetadataName(
            "Friflo.Engine.ECS.QueryAttribute",
            predicate: (node, _) => node is MethodDeclarationSyntax,
            // returning ctx (GeneratorAttributeSyntaxContext) has real disadvantages:
            // - Equality Check always fails (reference type)
            //   => incremental compiler is triggered on every keystroke. Caching disabled
            // - The compiler stores the heavy GeneratorAttributeSyntaxContext containing SemanticModel & entire Compilation
            //   => GC cannot collect this fat tree of objects 
            transform: (ctx, _) => ctx);
        
            context.RegisterSourceOutput(methodDeclarations, (productionContext, syntaxContext) => {
            var result = GenerateMethod(syntaxContext.SemanticModel, syntaxContext.TargetSymbol, GenerateTrigger.QueryAttribute);
            GeneratorUtils.EmitResult(productionContext, result);
        });
    }
    
    private static EmissionResult TransformAttribute(GeneratorAttributeSyntaxContext ctx, CancellationToken _, GenerateTrigger trigger)
    {
        Location? methodLocation = null;
        try {
            var targetSymbol = ctx.TargetSymbol;
            methodLocation = targetSymbol.Locations.FirstOrDefault();
            return GenerateMethod(ctx.SemanticModel, targetSymbol, trigger);
        } catch (Exception exception) {
            var exceptionMessage = $"{exception.GetType()} : {exception.Message}";
            return new EmissionResult(exceptionMessage, exception.StackTrace, methodLocation);
        }
    }
    
    private static EmissionResult GenerateMethod(SemanticModel semanticModel, ISymbol targetSymbol, GenerateTrigger trigger)
    {
        if (targetSymbol is not IMethodSymbol blueprintMethod) {
            return new EmissionResult("", "", []);
        }
        var diagnostics = new Diagnostics { BlueprintMethod = blueprintMethod };
        var attributes  = blueprintMethod.GetAttributes();
        
        bool hasQueryAttribute  = GeneratorUtils.HasAttribute    (attributes, "Friflo.Engine.ECS.QueryAttribute");
        var  vectorizeData      = GeneratorUtils.GetAttributeData(attributes, "Friflo.Vectorization.VectorizeAttribute");
        bool hasKernelAttribute = GeneratorUtils.HasAttribute    (attributes, "Friflo.Vectorization.GPU.KernelAttribute");

        VectorMode vectorMode;
        switch (trigger) {
            case GenerateTrigger.KernelAttribute:
                vectorMode = VectorMode.Vector;
                break;
            case GenerateTrigger.VectorizeAttribute:
                if (hasQueryAttribute || hasKernelAttribute) {
                    return new EmissionResult("", "", []); // already handled by GenerateTrigger: QueryAttribute or KernelAttribute
                }
                vectorMode = VectorMode.Vector;
                break;
            case GenerateTrigger.QueryAttribute:
                vectorMode = VectorMode.Query;
                break;
            default:
                return new EmissionResult("", "", []); // unreachable
        }
        // Get the symbol for the interfaces; ITag and IComponent
        var compilation         = semanticModel.Compilation;
        var className           = blueprintMethod.ContainingType.ToDisplayString(ClassNameFormat);
        var isGlobalNamespace   = blueprintMethod.ContainingNamespace.IsGlobalNamespace;
        var namespaceName       = blueprintMethod.ContainingType.ContainingNamespace.ToDisplayString();
        var parameters          = blueprintMethod.Parameters;
        var hash                = GetHash(blueprintMethod, attributes, compilation);
        var blueprintParameters = BlueprintParameter.CreateBlueprintParameters(parameters, vectorMode, compilation);
        var vectorTypes         = VectorType.GetVectorTypes(diagnostics, blueprintParameters);
        var spans               = BlueprintParameter.GetVectorSpans(blueprintParameters);
        var customMethod        = GetCustomMethod(vectorizeData);
        
        var query = new Query {
            BlueprintMethod = blueprintMethod,
            Diagnostics     = diagnostics,
            CustomMethod    = customMethod,
            VectorMode      = vectorMode,
            Attributes      = attributes,
            Parameters      = blueprintParameters.ToImmutableArray(),
            VectorTypes     = vectorTypes.ToImmutableArray(),
            Spans           = spans.ToImmutableArray(),
            Hash            = hash,
            SemanticModel   = semanticModel
        };
        bool hasVectorizeAttribute = vectorizeData != null;
        if (hasVectorizeAttribute) {
            new AvxVectorizer().Emit(query);
        }
        if (hasKernelAttribute) {
            new WgslVectorizer().Emit(query);
        }
        string vectorMethodSource;
        string kernelMethodSource = "";
        string kernelMethodPrivate = "";
        var privateSource = "";
        if (vectorMode == VectorMode.Query) {
            EmitQuerySource(query, out vectorMethodSource, out privateSource);
        } else {
            if (hasVectorizeAttribute && !query.vectorized) {
                return new EmissionResult("", "", query.Diagnostics.List);
            }
            vectorMethodSource = EmitVectorSource(query);
            if (hasKernelAttribute) {
                kernelMethodSource  = EmitKernelSource(query);
                kernelMethodPrivate = EmitKernelPrivate(query);
            }
        }
        var namespaces          = EmitNamespaces(query, hasKernelAttribute, hasVectorizeAttribute);

        // ----------------- General code generation
        var source = $@"// <auto-generated/>
{namespaces}
{(isGlobalNamespace ? "" : $"namespace {namespaceName}\r\n{{")}
    public partial class {className}
    {{{kernelMethodSource}{vectorMethodSource}

    #region private members{privateSource}
{(hasVectorizeAttribute ? query.avxMethod : "")}{kernelMethodPrivate}
    #endregion
    }}
{(isGlobalNamespace ? "" : "}")}
";
        var fileName = GeneratorUtils.CreateFileName(blueprintMethod, hash);

        return new EmissionResult(fileName, source, query.Diagnostics.List);
    }
    
    
    private static readonly SymbolDisplayFormat ClassNameFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters
    );

    
    // made required static symbols unique to prevent duplicate symbol names
    private static string GetHash(IMethodSymbol methodSymbol, ImmutableArray<AttributeData> attributes, Compilation compilation)
    {
        var omitHashAttribute = compilation.GetTypeByMetadataName("Friflo.Vectorization.OmitHashAttribute")!;
        var search = omitHashAttribute.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        bool found = false;
        foreach (var attributeData in attributes) {
            // if (SymbolEqualityComparer.Default.Equals(ecsTypes.omitHashAttribute, attributeData.AttributeClass)) found = true;
            if (attributeData.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == search) found = true;
        }
        if (found) {
            return "";
        }
        var methodSignature = methodSymbol.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);
        return "_" + GeneratorUtils.GetMd5Hash(methodSignature).Substring(0, 4); // 8 chars is usually enough
    }
    

    
    private static string EmitNamespaces(Query query, bool hasKernelAttribute, bool hasVectorizeAttribute)
    {
        var sb = new StringBuilder();
        sb.AppendLine($@"using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;");

        if (hasVectorizeAttribute && query.vectorized) {
            sb.AppendLine(@"using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using Friflo.Vectorization.Intrinsics;");
        }
        
        if (hasKernelAttribute)
        {
            sb.AppendLine(@"using System.Collections.Generic;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.GPU.Runtime;
using Friflo.Vectorization.WebGPU;
using Friflo.Vectorization.WebGPU.Runtime;");
        }
        
        if (query.VectorMode == VectorMode.Query) {
            sb.AppendLine(@"using Friflo.Engine.ECS;");
        }
        return sb.ToString();
    }
    
    private static string? GetCustomMethod(AttributeData? vectorizeData)
    {
        if (vectorizeData == null) return null;
        var args = vectorizeData.ConstructorArguments;
        if (args.Length > 0) {
            var value = args[0].Value;
            return value as string;
        }
        return null;
    }
}

