// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Threading;
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
            Utils.AddSource(ctx, "AvxVector2.g.cs");
            Utils.AddSource(ctx, "AvxVector3.g.cs");
            Utils.AddSource(ctx, "AvxVector4.g.cs");
            Utils.AddSource(ctx, "MathUtils.g.cs");
            Utils.AddSource(ctx, "VectorUtils.g.cs");
        });
    }
    
    private static void RegisterStreamingTranspiler(IncrementalGeneratorInitializationContext context)
    {
        // Filter for methods with the attribute
        var queryMethod = context.SyntaxProvider.ForAttributeWithMetadataName(
            "Friflo.Engine.ECS.QueryAttribute",
            predicate: (node, _) => node is MethodDeclarationSyntax,
            transform: (ctx, ct) => TransformAttribute(ctx, ct, GenerateTrigger.QueryAttribute));
        context.RegisterSourceOutput(queryMethod, EmitResult);
        
        var vectorizeMethod = context.SyntaxProvider.ForAttributeWithMetadataName(
            "Friflo.Vectorization.VectorizeAttribute",
            predicate: (node, _) => node is MethodDeclarationSyntax,
            transform: (ctx, ct) => TransformAttribute(ctx, ct, GenerateTrigger.VectorizeAttribute));
        context.RegisterSourceOutput(vectorizeMethod, EmitResult);
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
            EmitResult(productionContext, result);
        });
    }

    
    
    private static void EmitResult(SourceProductionContext  productionContext, EmissionResult emissionResult)
    {
        foreach (var data in emissionResult.diagnostics) {
            var start       = new LinePosition(data.StartLine, data.StartColumn);
            var end         = new LinePosition(data.EndLine, data.EndColumn);
            var lineSpan    = new LinePositionSpan(start, end);
            var location    = Location.Create(data.FilePath, new TextSpan(data.StartOffset, data.Length), lineSpan);
            Diagnostic diagnostic = Diagnostic.Create(data.Descriptor, location, data.MessageArgs);
            productionContext.ReportDiagnostic(diagnostic);
        }
        if (emissionResult.code == "") {
            return;
        }
        productionContext.AddSource(emissionResult.name, SourceText.From(emissionResult.code, Encoding.UTF8));
    }
    
    private static EmissionResult TransformAttribute(GeneratorAttributeSyntaxContext ctx, CancellationToken _, GenerateTrigger trigger) {
        return GenerateMethod(ctx.SemanticModel, ctx.TargetSymbol, trigger);
    }
    
    private static EmissionResult GenerateMethod(SemanticModel semanticModel, ISymbol targetSymbol, GenerateTrigger trigger)
    {
        if (targetSymbol is not IMethodSymbol blueprintMethod) {
            return new EmissionResult("", "", []);
        }
        VectorMode vectorMode;
        var attributes = blueprintMethod.GetAttributes();
        bool hasQueryAttribute      = Utils.HasAttribute(attributes, "Friflo.Engine.ECS.QueryAttribute");
        var  vectorizeData          = Utils.GetAttributeData(attributes, "Friflo.Vectorization.VectorizeAttribute");

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
        var types = new NamedTypes {
            componentInterface  = compilation.GetTypeByMetadataName("Friflo.Engine.ECS.IComponent"),
            entityStruct        = compilation.GetTypeByMetadataName("Friflo.Engine.ECS.Entity"),
            omitHashAttribute   = compilation.GetTypeByMetadataName("Friflo.Vectorization.OmitHashAttribute"),
        };

        var className           = blueprintMethod.ContainingType.ToDisplayString(ClassNameFormat);
        var isGlobalNamespace   = blueprintMethod.ContainingNamespace.IsGlobalNamespace;
        var namespaceName       = blueprintMethod.ContainingType.ContainingNamespace.ToDisplayString();
        var parameters          = blueprintMethod.Parameters;
        var spans               = GetVectorSpans(parameters, types, vectorMode);
        var hash                = GetHash(blueprintMethod, attributes, types);
        var diagnostics         = new Diagnostics { BlueprintMethod = blueprintMethod };
        var blueprintParameters = GetBlueprintParameters(parameters, vectorMode, vectorizeData != null, types);
        var vectorTypes         = VectorType.GetVectorTypes(diagnostics, blueprintParameters, vectorizeData != null);
        var spans2              = GetVectorSpans2(blueprintParameters);
        var query = new Query {
            BlueprintMethod = blueprintMethod,
            Diagnostics     = diagnostics,
            CustomMethod    = GetCustomMethod(vectorizeData),
            VectorMode      = vectorMode,
            Attributes      = attributes,
            Parameters      = blueprintParameters,
            VectorTypes     = vectorTypes,
            Spans           = spans,
            Span2          = spans2,
            Hash            = hash,
            NamedTypes      = types,
            SemanticModel   = semanticModel
        };
        if (vectorizeData != null) {
            Vectorizer.Emit(query);
        }
        string shadowMethodSource;
        var privateSource = "";
        if (vectorMode == VectorMode.Query) {
            EmitQuerySource(query, out shadowMethodSource, out privateSource);
        } else {
            if (!query.vectorized) {
                return new EmissionResult("", "", query.Diagnostics.list);
            }
            EmitVectorSource(query, out shadowMethodSource);
        }
        var namespaces          = EmitNamespaces(query);

        // ----------------- General code generation
        var source = $@"// <auto-generated/>
{namespaces}

{(isGlobalNamespace ? "" : $"namespace {namespaceName}\r\n{{")}
    public partial class {className}
    {{{shadowMethodSource}

    #region private members{privateSource}
{query.avxMethod}
    #endregion
    }}
{(isGlobalNamespace ? "" : "}")}
";
        var fileName = CreateFileName(blueprintMethod, hash);

        return new EmissionResult(fileName, source, query.Diagnostics.list);
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
    private static string GetHash(IMethodSymbol methodSymbol, ImmutableArray<AttributeData> attributes, NamedTypes namedTypes)
    {
        var search = namedTypes.omitHashAttribute.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
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
    
    private static string CreateFileName(IMethodSymbol methodSymbol, string hash)
    {
        var fileName =
            methodSymbol.ContainingType.ToDisplayString().Replace('<', '{').Replace('>', '}') + "/" +
            methodSymbol.ToDisplayString(FullNameFormat).Replace('<', '{').Replace('>', '}');
        // using hash instead of method signature for file name. Signature would lead to long file names not supported by the OS
        return $"{fileName}{hash}.g.cs";
    }
    
    private static string EmitNamespaces(Query query)
    {
        var intrinsics = "";
        if (query.vectorized) {
            intrinsics =@"
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using Friflo.Vectorization.Intrinsics;";
        }
        var source =
$@"using System;
using System.ComponentModel;{intrinsics}
{(query.VectorMode == VectorMode.Query ? "using Friflo.Engine.ECS;" : "")}";
        return source;
    }
    
    private static BlueprintParameter[] GetBlueprintParameters(
        ImmutableArray<IParameterSymbol>    parameters,
        VectorMode                          vectorMode,
        bool                                vectorize,
        NamedTypes                          namedTypes)
    {
        var blueprintParam = new BlueprintParameter[parameters.Length];
        for (int n = 0; n < parameters.Length; n++) {
            var symbol = parameters[n];
            var vectorType = VectorType.GetVectorType(symbol, vectorMode, namedTypes);
            bool isSpan = vectorMode switch {
                VectorMode.Query    => namedTypes.IsComponent(symbol.Type),
                VectorMode.Vector   => Utils.HasAttribute(symbol.GetAttributes(), "Friflo.Vectorization.SpanAttribute"),
                _                   => false
            };
            blueprintParam[n] = new BlueprintParameter{ Symbol = symbol, VectorType = vectorType, IsSpan = isSpan };
        }
        return blueprintParam;
    }
    
    private static List<IParameterSymbol> GetVectorSpans(ImmutableArray<IParameterSymbol> parameters,
        NamedTypes namedTypes, VectorMode vectorMode)
    {
        var result = new List<IParameterSymbol>();
        foreach (var parameter in parameters)
        {
            bool isSpan = vectorMode switch {
                VectorMode.Query    => namedTypes.IsComponent(parameter.Type),
                VectorMode.Vector   => Utils.HasAttribute(parameter.GetAttributes(), "Friflo.Vectorization.SpanAttribute"),
                _                   => false
            };
            if (isSpan) {
                result.Add(parameter);   
            }
        }
        return result;
    }
    
    private static BlueprintParameter[] GetVectorSpans2(BlueprintParameter[] parameters)
    {
        var result = new List<BlueprintParameter>();
        foreach (var parameter in parameters)
        {
            if (parameter.IsSpan) {
                result.Add(parameter);
            }
        }
        return result.ToArray();
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

