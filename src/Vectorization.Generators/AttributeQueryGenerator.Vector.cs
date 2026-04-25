// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Text;
using Microsoft.CodeAnalysis;

namespace Friflo.Vectorization.Generators;

public partial class AttributeQueryGenerator
{
    private static void EmitVectorSource(
        Query query,
        out string shadowMethodSource)
    {
        var lambdaParameters    = EmitVectorLambdaParameters(query.VectorTypes);
        var methodSignature     = EmitVectorMethodSignature(query.VectorTypes, query.vectorized);
        var vectorizeBlock      = EmitVectorBlock(query);
        
        var blueprintMethod = query.BlueprintMethod;
        var methodName      = query.BlueprintMethod.Name;
        
            shadowMethodSource = $@"
        /// <summary>Vector method generated for: <see cref=""{methodName}""/>.</summary>
        public {(blueprintMethod.IsStatic ? "static " : "")}void {methodName}Vector({methodSignature})
        {{
            int count = {query.VectorTypes[0].Parameter.Name}.Length;
            int n = 0;{vectorizeBlock}
            for (; n < count; n++) {{
                {methodName}({lambdaParameters});
            }}
        }}";
    }
    
    private static string EmitVectorBlock(Query query)
    {
        if (!query.vectorized) {
            return "";
        }
        var sb = new StringBuilder();
        sb.Append("count");
        foreach (var vectorType in query.VectorTypes) {
            sb.Append(", ");
            var parameter = vectorType.Parameter;
            if (vectorType.IsSpan) {
                sb.Append(parameter.Name);
                continue;
            }
            Utils.AppendRefKind(sb, parameter.RefKind);
            sb.Append(parameter.Name);
        }
        var avxMethod = query.CustomMethod ?? $"_{query.BlueprintMethod.Name}_Avx{query.Hash}";
        var source = $@"
            if (vectorized) {{
                if (Avx.IsSupported) {{
                    n = {avxMethod}({sb});
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
            var parameter = vectorType.Parameter;
            string type = parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (vectorType.IsSpan) {
                var span = parameter.RefKind == RefKind.Ref ? "Span" : "ReadOnlySpan";
                sb.Append($"{span}<{type}> {parameter.Name}");
                continue;
            }
            Utils.AppendRefKind(sb, parameter.RefKind);
            sb.Append($"{type} {parameter.Name}");
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
            var parameter = vectorType.Parameter;
            Utils.AppendRefKind(sb, parameter.RefKind);
            if (vectorType.IsSpan) {
                sb.Append($"{parameter.Name}[n]");
                continue;
            }
            sb.Append(parameter.Name);
        }
        return sb.ToString();
    }
}