// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Text;
using Microsoft.CodeAnalysis;

namespace Friflo.Vectorization.Generators;

public partial class AttributeQueryGenerator
{
    private static void EmitVectorSource(
        Query query,
        out string vectorMethod)
    {
        var lambdaParameters    = EmitVectorLambdaParameters(query.vectorTypes);
        var methodSignature     = EmitVectorMethodSignature(query.vectorTypes, query.vectorize);
        var vectorizeBlock      = EmitVectorBlock(query);
        
        var methodSymbol    = query.methodSymbol;
        var methodName      = query.methodSymbol.Name;
        
            vectorMethod = $@"
        /// <summary>Vector method generated for: <see cref=""{methodName}""/>.</summary>
        public {(methodSymbol.IsStatic ? "static " : "")}void {methodName}Vector({methodSignature})
        {{
            int n = 0;{vectorizeBlock}
            int len = {query.vectorTypes[0].parameter.Name}.Length;
            for (; n < len; n++) {{
                {methodName}({lambdaParameters});
            }}
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
                continue;
            }
            Utils.AppendRefKind(sb, parameter.RefKind);
            sb.Append(parameter.Name);
        }
        var source = $@"
            if (vectorized) {{
                if (Avx.IsSupported) {{
                    n = _{query.methodSymbol.Name}_Avx{query.hash}({sb});
                }}
            }}";
        return source;
    }
    
    private static string EmitVectorMethodSignature(VectorType[] vectorTypes, bool vectorized)
    {
        var sb = new StringBuilder();
        foreach (var vectorType in vectorTypes) {
            if (sb.Length > 0) {
                sb.Append(", ");
            }
            string type = vectorType.parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (vectorType.isSpan) {
                sb.Append($"Span<{type}> {vectorType.parameter.Name}");
                continue;
            }
            Utils.AppendRefKind(sb, vectorType.parameter.RefKind);
            sb.Append($"{type} {vectorType.parameter.Name}");
        }
        if (vectorized) {
            sb.Append(", bool vectorized = true");
        }
        return sb.ToString();
    }
    
    private static string EmitVectorLambdaParameters(VectorType[] vectorTypes)
    {
        var sb = new StringBuilder();
        foreach (var vectorType in vectorTypes) {
            if (sb.Length > 0) {
                sb.Append(", ");
            }
            Utils.AppendRefKind(sb, vectorType.parameter.RefKind);
            if (vectorType.isSpan) {
                sb.Append($"{vectorType.parameter.Name}[n]");
                continue;
            }
            sb.Append(vectorType.parameter.Name);
        }
        return sb.ToString();
    }
}