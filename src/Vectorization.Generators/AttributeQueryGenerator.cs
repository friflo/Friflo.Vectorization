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
        // Filter for methods with the attribute
        var methodDeclarations = context.SyntaxProvider.ForAttributeWithMetadataName(
            "Friflo.Engine.ECS.QueryAttribute",
            predicate: (node, _) => node is MethodDeclarationSyntax,
            transform: (ctx, _) => ctx
        );
        context.RegisterPostInitializationOutput(ctx => {
            ctx.AddSource("Friflo.Vectorization.Intrinsics/AvxUtils.g.cs",     Static.Code);
            ctx.AddSource("Friflo.Vectorization.Intrinsics/AvxVector2.g.cs",   Static.AvxVector2);
            ctx.AddSource("Friflo.Vectorization.Intrinsics/AvxVector3.g.cs",   Static.AvxVector3);
            ctx.AddSource("Friflo.Vectorization.Intrinsics/AvxVector4.g.cs",   Static.AvxVector4);
        });

        // Generate the source
        context.RegisterSourceOutput(methodDeclarations, (spc, ctx) =>
        {
            // Get the symbol for the interfaces; ITag and IComponent
            var compilation = ctx.SemanticModel.Compilation;
            var types = new EcsTypes {
                componentInterface  = compilation.GetTypeByMetadataName("Friflo.Engine.ECS.IComponent"),
                entityStruct        = compilation.GetTypeByMetadataName("Friflo.Engine.ECS.Entity"),
                vectorizeAttribute  = compilation.GetTypeByMetadataName("Friflo.Vectorization.VectorizeAttribute"),
                omitHashAttribute   = compilation.GetTypeByMetadataName("Friflo.Vectorization.OmitHashAttribute"),
            };
            var methodSymbol        = (IMethodSymbol)ctx.TargetSymbol;
            var className           = methodSymbol.ContainingType.ToDisplayString(ClassNameFormat);
            var methodName          = methodSymbol.Name;
            var isGlobalNamespace   = methodSymbol.ContainingNamespace.IsGlobalNamespace;
            var namespaceName       = methodSymbol.ContainingType.ContainingNamespace.ToDisplayString();
            var attributes          = methodSymbol.GetAttributes();
            var attributeCode       = EmitQueryFilters(attributes);
            var parameters          = methodSymbol.Parameters;
            var components          = GetQueryComponents(parameters, types);
            var hash                = GetHash(methodSymbol, attributes, types);
            var query = new Query {
                methodSymbol    = methodSymbol,
                attributes      = attributes,
                parameters      = parameters, 
                components      = components,
                hash            = hash,
                ecsTypes        = types,
                spc             = spc,
                semanticModel   = ctx.SemanticModel
            };
            Vectorizer.Emit(query);
            var componentArgs       = EmitQueryArgs(components);
            var chunkVariables      = EmitQueryChunkVariables(components);
            var lambdaParameters    = EmitQueryLambdaParameters(parameters, types);
            var methodSignature     = EmitQueryMethodSignature(parameters, types, query.vectorize);
            var vectorizeBlock      = Vectorizer.EmitVectorizeBlock(query);
            var namespaces          = EmitNamespaces(query);
            
            // ----------------- ECS specific code generation
            var ecsQueryMethod = $@"
        /// <summary>Query method generated for: <see cref=""{methodName}""/>.</summary>
        /// <returns>The executed <see cref=""ArchetypeQuery""/> for debugging purposes</returns>
        public {(methodSymbol.IsStatic ? "static " : "")}ArchetypeQuery {methodName}Query({methodSignature})
        {{
            var _query = _{methodName}_GetQuery{hash}(_store);
            foreach (var chunk in _query.Chunks)
            {{
                var _entities = chunk.Entities;
{chunkVariables}
                int n = 0;{vectorizeBlock}
                for (; n < _entities.Length; n++) {{
                    {methodName}({lambdaParameters});
                }}
            }}
            return _query;
        }}";
            var ecsQueryPrivate = $@"
        [EditorBrowsable(EditorBrowsableState.Never)]
        private static readonly int _{methodName}_Slot{hash} = EntityStore.UserDataNewSlot();

        [EditorBrowsable(EditorBrowsableState.Never)]
        private static ArchetypeQuery<{componentArgs}>
            _{methodName}_GetQuery{hash}(EntityStore _store)
        {{
            var _query = (ArchetypeQuery<{componentArgs}>)
                EntityStore.UserDataGet(_store, _{methodName}_Slot{hash});
            if (_query != null) {{
                return _query;
            }}
            _query = _store.Query<{componentArgs}>();
{attributeCode}
            EntityStore.UserDataSet(_store, _{methodName}_Slot{hash}, _query);
            return _query;
        }}";

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
            spc.AddSource($"{fileName}{hash}.g.cs", SourceText.From(source, Encoding.UTF8));
        });
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

