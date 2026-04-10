// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Friflo.Vectorization.Generators;

[Generator]
public partial class AttributeQueryGenerator : IIncrementalGenerator
{
    // --- IIncrementalGenerator
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
#if DEBUG_XXX
        if (!System.Diagnostics.Debugger.IsAttached)
        {
            System.Diagnostics.Debugger.Launch();
        }
#endif
        RegisterStreamingTranspiler(context);
        // RegisterTranspiler_BadCommonApproach(context);
        
        context.RegisterPostInitializationOutput(ctx => {
            ctx.AddSource("Friflo.Vectorization.Intrinsics/AvxUtils.g.cs",     Static.Code);
            ctx.AddSource("Friflo.Vectorization.Intrinsics/AvxVector2.g.cs",   Static.AvxVector2);
            ctx.AddSource("Friflo.Vectorization.Intrinsics/AvxVector3.g.cs",   Static.AvxVector3);
            ctx.AddSource("Friflo.Vectorization.Intrinsics/AvxVector4.g.cs",   Static.AvxVector4);
        });
    }
    
    private static void RegisterStreamingTranspiler(IncrementalGeneratorInitializationContext context)
    {
        // Filter for methods with the attribute
        var queryMethod = context.SyntaxProvider.ForAttributeWithMetadataName(
            "Friflo.Engine.ECS.QueryAttribute",
            predicate: (node, _) => node is MethodDeclarationSyntax,
            transform: (ctx, ct) => GenerateMethod(ctx.SemanticModel, ctx.TargetSymbol, GenerateTrigger.QueryAttribute));
        context.RegisterSourceOutput(queryMethod, (productionContext, emissionResult) => {
            EmitResult(productionContext, emissionResult);
        });
        
        var vectorizeMethod = context.SyntaxProvider.ForAttributeWithMetadataName(
            "Friflo.Vectorization.VectorizeAttribute",
            predicate: (node, _) => node is MethodDeclarationSyntax,
            transform: (ctx, ct) => GenerateMethod(ctx.SemanticModel, ctx.TargetSymbol, GenerateTrigger.VectorizeAttribute));
        context.RegisterSourceOutput(vectorizeMethod, (productionContext, emissionResult) => {
            EmitResult(productionContext, emissionResult);
        });
    }
    
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
            transform: (ctx, ct) => ctx);
        
            context.RegisterSourceOutput(methodDeclarations, (productionContext, syntaxContext) => {
            var result = GenerateMethod(syntaxContext.SemanticModel, syntaxContext.TargetSymbol, GenerateTrigger.QueryAttribute);
            EmitResult(productionContext, result);
        });
    }

    
    
    private static void EmitResult(SourceProductionContext  productionContext, EmissionResult emissionResult)
    {
        if (emissionResult.code == "") {
            return;
        }
        foreach (var data in emissionResult.diagnostics) {
            var start       = new LinePosition(data.StartLine, data.StartColumn);
            var end         = new LinePosition(data.EndLine, data.EndColumn);
            var lineSpan    = new LinePositionSpan(start, end);
            var location    = Location.Create(data.FilePath, new TextSpan(data.StartOffset, data.Length), lineSpan);
            Diagnostic diagnostic = Diagnostic.Create(data.Descriptor, location, data.MessageArgs);
            productionContext.ReportDiagnostic(diagnostic);
        }
        productionContext.AddSource(emissionResult.name, SourceText.From(emissionResult.code, Encoding.UTF8));
    }
    
    private static EmissionResult GenerateMethod(SemanticModel semanticModel, ISymbol targetSymbol, GenerateTrigger trigger)
    {
        if (targetSymbol is not IMethodSymbol methodSymbol) {
            return new EmissionResult("", "", []);
        }
        var vectorMode = VectorMode.None;
        var attributes = methodSymbol.GetAttributes();
        bool hasQueryAttribute      = Utils.HasAttribute(attributes, "Friflo.Engine.ECS.QueryAttribute");
        bool hasVectorizeAttribute  = Utils.HasAttribute(attributes, "Friflo.Vectorization.VectorizeAttribute");
        if (trigger == GenerateTrigger.VectorizeAttribute) {
            if (hasQueryAttribute) {
                return new EmissionResult("", "", []); // already handled by GenerateTrigger.QueryAttribute
            }
            vectorMode = VectorMode.Vector;
        } else {
            vectorMode = VectorMode.Query;
        }
        // Get the symbol for the interfaces; ITag and IComponent
        var compilation = semanticModel.Compilation;
        var types = new EcsTypes {
            componentInterface  = compilation.GetTypeByMetadataName("Friflo.Engine.ECS.IComponent"),
            entityStruct        = compilation.GetTypeByMetadataName("Friflo.Engine.ECS.Entity"),
            vectorizeAttribute  = compilation.GetTypeByMetadataName("Friflo.Vectorization.VectorizeAttribute"),
            omitHashAttribute   = compilation.GetTypeByMetadataName("Friflo.Vectorization.OmitHashAttribute"),
        };

        var className           = methodSymbol.ContainingType.ToDisplayString(ClassNameFormat);
        var isGlobalNamespace   = methodSymbol.ContainingNamespace.IsGlobalNamespace;
        var namespaceName       = methodSymbol.ContainingType.ContainingNamespace.ToDisplayString();
        var parameters          = methodSymbol.Parameters;
        var spans               = GetVectorSpans(parameters, types, vectorMode);
     // var spans               = GetVectorSpans(parameters, types);
        var hash                = GetHash(methodSymbol, attributes, types);
        var query = new Query {
            methodSymbol            = methodSymbol,
            vectorMode              = vectorMode,
            hasQueryAttribute       = hasQueryAttribute,
            hasVectorizeAttribute   = hasVectorizeAttribute,
            attributes              = attributes,
            parameters              = parameters, 
            components              = spans,
            hash                    = hash,
            ecsTypes                = types,
            semanticModel           = semanticModel
        };
        Vectorizer.Emit(query);
        
        EmitQuerySource(query, out string ecsQueryMethod, out string ecsQueryPrivate);
        
        var namespaces          = EmitNamespaces(query);

        // ----------------- General code generation
        var source = $@"// <auto-generated/>
{namespaces}

{(isGlobalNamespace ? "" : $"namespace {namespaceName}\r\n{{")}
    public partial class {className}
    {{{ecsQueryMethod}

    #region private members{ecsQueryPrivate}
{query.avxMethod}
    #endregion
    }}
{(isGlobalNamespace ? "" : "}")}
";
        var fileName =
            methodSymbol.ContainingType.ToDisplayString().Replace('<', '{').Replace('>', '}') + "/" +
            methodSymbol.ToDisplayString(FullNameFormat).Replace('<', '{').Replace('>', '}');
        // using hash instead of method signature for file name. Signature would lead to long file names not supported by the OS
        fileName = $"{fileName}{hash}.g.cs";
        return new EmissionResult(fileName, source, query.diagnostics);
    }
    
    
    
    
    
    private static readonly SymbolDisplayFormat ClassNameFormat = new SymbolDisplayFormat(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters
    );
    private static readonly SymbolDisplayFormat FullNameFormat = new SymbolDisplayFormat(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        // memberOptions: SymbolDisplayMemberOptions.IncludeContainingType,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType // Include this if you want (string x, int y)
    );
    
    // made required static symbols unique to prevent duplicate symbol names
    private static string GetHash(IMethodSymbol methodSymbol, ImmutableArray<AttributeData> attributes, EcsTypes ecsTypes)
    {
        var search = ecsTypes.omitHashAttribute.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        bool found = false;
        foreach (var attributeData in attributes) {
            // if (SymbolEqualityComparer.Default.Equals(ecsTypes.omitHashAttribute, attributeData.AttributeClass)) found = true;
            if (attributeData.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == search) found = true;
        }
        if (found) {
            return "";
        }
        var methodSignature = methodSymbol.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);
        return "_" + Utils.GetMd5Hash(methodSignature).Substring(0, 4); // 8 chars is usually enough
    }
    
    private static string EmitNamespaces(Query query)
    {
        var intrinsics = "";
        if (query.vectorize) {
            intrinsics =@"
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using Friflo.Vectorization.Intrinsics;";
        }
        var source =
$@"using System;
using System.ComponentModel;{intrinsics}
using Friflo.Engine.ECS;";
        return source;
    }
}

