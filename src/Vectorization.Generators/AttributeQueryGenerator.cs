// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
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
        // Filter for methods with the attribute
        var methodDeclarations = context.SyntaxProvider.ForAttributeWithMetadataName(
            "Friflo.Engine.ECS.QueryAttribute",
            predicate: (node, _) => node is MethodDeclarationSyntax,
            transform: (ctx, ct) => {
                return ctx;
            });
        context.RegisterSourceOutput(methodDeclarations, (productionContext, syntaxContext) => {
            var symbol = syntaxContext.TargetSymbol;
            if (symbol is not IMethodSymbol methodSymbol) {
                return;
            }
            var emissionResult = GenerateMethod(syntaxContext.SemanticModel, methodSymbol);
            foreach (var data in emissionResult.Diagnostics) {
                var start       = new LinePosition(data.StartLine, data.StartColumn);
                var end         = new LinePosition(data.EndLine, data.EndColumn);
                var lineSpan    = new LinePositionSpan(start, end);
                var location    = Location.Create(data.FilePath, new TextSpan(data.StartOffset, data.Length), lineSpan);
                Diagnostic diagnostic = Diagnostic.Create(data.Descriptor, location, data.MessageArgs);
                productionContext.ReportDiagnostic(diagnostic);
            }
            productionContext.AddSource(emissionResult.Name, SourceText.From(emissionResult.Code, Encoding.UTF8));
        });
        
        /* var methodDeclarations = context.SyntaxProvider.ForAttributeWithMetadataName(
            "Friflo.Engine.ECS.QueryAttribute",
            predicate: (node, _) => node is MethodDeclarationSyntax,
            transform: (ctx, ct) => {
                if (ctx.TargetSymbol is  IMethodSymbol methodSymbol) {
                    // GenerateMethod(null, ctx.SemanticModel, methodSymbol);
                }
                return new EmissionResult("", "");
            });
        context.RegisterSourceOutput(methodDeclarations, (productionContext, emissionResult) => {
            // GenerateMethod(productionContext, syntaxContext.SemanticModel, methodSymbol);
        }); */
        
        context.RegisterPostInitializationOutput(ctx => {
            ctx.AddSource("Friflo.Vectorization.Intrinsics/AvxUtils.g.cs",     Static.Code);
            ctx.AddSource("Friflo.Vectorization.Intrinsics/AvxVector2.g.cs",   Static.AvxVector2);
            ctx.AddSource("Friflo.Vectorization.Intrinsics/AvxVector3.g.cs",   Static.AvxVector3);
            ctx.AddSource("Friflo.Vectorization.Intrinsics/AvxVector4.g.cs",   Static.AvxVector4);
        });
    }
    
    private static EmissionResult GenerateMethod(SemanticModel semanticModel, IMethodSymbol methodSymbol)
    {
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
        var attributes          = methodSymbol.GetAttributes();
        var parameters          = methodSymbol.Parameters;
        var components          = GetQueryComponents(parameters, types);
     // var spans               = GetVectorSpans(parameters, types);
        var hash                = GetHash(methodSymbol, attributes, types);
        var query = new Query {
            methodSymbol    = methodSymbol,
            attributes      = attributes,
            parameters      = parameters, 
            components      = components,
            hash            = hash,
            ecsTypes        = types,
            semanticModel   = semanticModel
        };
        Vectorizer.Emit(query);
        
        EmitQuerySource(query, out string ecsQueryMethod, out string ecsQueryPrivate);
        
        var namespaces          = EmitNamespaces(query);
        
        // ----------------- ECS specific code generation


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

