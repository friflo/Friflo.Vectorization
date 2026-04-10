// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Friflo.Vectorization.Generators;

public partial class AttributeQueryGenerator
{
    private static void EmitVectorSource(
        Query query,
        out string vectorMethod)
    {
        var lambdaParameters    = EmitVectorLambdaParameters(query.parameters, query.ecsTypes);
        var methodSignature     = EmitVectorMethodSignature(query.parameters, query.ecsTypes, query.vectorize);
        var vectorizeBlock      = EmitVectorBlock(query);
        
        var hash            = query.hash;
        var methodSymbol    = query.methodSymbol;
        var methodName      = query.methodSymbol.Name;
        
            vectorMethod = $@"
        /// <summary>Vector method generated for: <see cref=""{methodName}""/>.</summary>
        public {(methodSymbol.IsStatic ? "static " : "")}ArchetypeQuery {methodName}Query({methodSignature})
        {{
            var _query = _{methodName}_GetQuery{hash}(_store);
            foreach (var chunk in _query.Chunks)
            {{
                var _entities = chunk.Entities;

                int n = 0;{vectorizeBlock}
                for (; n < _entities.Length; n++) {{
                    {methodName}({lambdaParameters});
                }}
            }}
            return _query;
        }}";
    }
    
    private static string EmitVectorBlock(Query query)
    {
        if (!query.vectorize) {
            return "";
        }
        var sb = new StringBuilder();
        foreach (var vectorType in query.vectorTypes) {
            if (sb.Length > 0) {
                sb.Append(", ");
            }
            var parameter = vectorType.parameter;
            if (vectorType.isSpan) {
                sb.Append(parameter.Name);
                sb.Append("Span");
                continue;
            }
            Utils.AppendRefKind(sb, parameter.RefKind);
            sb.Append(parameter.Name);
        }
        var source = $@"
                if (!vectorized) goto EntityLoop;
                if (Avx.IsSupported) {{
                    n = _{query.methodSymbol.Name}_Avx{query.hash}({sb});
                }}
            EntityLoop:";
        return source;
    }
    
    private static string EmitVectorMethodSignature(ImmutableArray<IParameterSymbol> parameters, EcsTypes ecsTypes, bool vectorized)
    {
        var sb = new StringBuilder();
        sb.Append("EntityStore _store");
        foreach (var parameter in parameters) {
            bool isComponent = ecsTypes.IsComponent(parameter.Type);
            if (isComponent) {
                continue;
            }
            bool isEntity = ecsTypes.IsEntityParameter(parameter);
            if (isEntity) {
                continue;
            }
            sb.Append(", ");
            Utils.AppendRefKind(sb, parameter.RefKind);
            string type = parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            sb.Append(type);
            sb.Append(" ");
            sb.Append(parameter.Name);
        }
        if (vectorized) {
            sb.Append(", bool vectorized = true");
        }
        return sb.ToString();
    }
    
    private static string EmitVectorLambdaParameters(ImmutableArray<IParameterSymbol> parameters, EcsTypes ecsTypes)
    {
        var sb = new StringBuilder();
        foreach (var parameter in parameters) {
            if (sb.Length > 0) {
                sb.Append(", ");
            }
            bool isComponent = ecsTypes.IsComponent(parameter.Type);
            if (isComponent) {
                Utils.AppendRefKind(sb, parameter.RefKind);
                sb.Append(parameter.Name);
                sb.Append("Span[n]");
                continue;
            }
            bool isEntity = ecsTypes.IsEntityParameter(parameter); 
            if (isEntity) {
                sb.Append("_entities.EntityAt(n)");
                continue;
            }
            Utils.AppendRefKind(sb, parameter.RefKind);
            sb.Append(parameter.Name);
        }
        return sb.ToString();
    }
    
    private static List<IParameterSymbol> GetVectorSpans(ImmutableArray<IParameterSymbol> parameters, EcsTypes ecsTypes)
    {
        var result = new List<IParameterSymbol>();
        foreach (var parameter in parameters)
        {
            bool isComponent = ecsTypes.IsComponent(parameter.Type);
            if (isComponent) {
                result.Add(parameter);   
            }
        }
        return result;
    }
}