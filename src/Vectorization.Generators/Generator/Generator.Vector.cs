// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Text;
using Friflo.Vectorization.Generators;
using Microsoft.CodeAnalysis;

// ReSharper disable once CheckNamespace
// Note: Used small namespace and class name to enable shorter path names in 'Generated' folders
namespace Friflo;

public sealed partial class Gen
{
    private static string EmitVectorSource(Query query)
    {
        var vectorTypes = query.VectorTypes;
        var vectorized  = query.vectorized;
        var lambdaParameters    = new StringBuilder();
        var methodSignature     = new StringBuilder();
        var avxParameters       = new StringBuilder();
        avxParameters.Append("count");
        
        foreach (var vectorType in vectorTypes)
        {
            var parameter = vectorType.Parameter;
            var type = vectorType.FullQualifiedName;
            GeneratorUtils.AppendRefKind(lambdaParameters, parameter.RefKind);
            avxParameters.Append(", ");
            if (vectorType.IsSpan) {
                lambdaParameters.Append($"{parameter.Name}[n], ");
                var span = parameter.RefKind == RefKind.Ref ? "Span" : "ReadOnlySpan";
                methodSignature.Append($"{span}<{type}> {parameter.Name}, ");
                avxParameters.Append(parameter.Name);
                continue;
            }
            lambdaParameters.Append($"{parameter.Name}, ");
            
            GeneratorUtils.AppendRefKind(methodSignature, parameter.RefKind);
            methodSignature.Append($"{type} {parameter.Name}, ");
            
            GeneratorUtils.AppendRefKind(avxParameters, parameter.RefKind);
            avxParameters.Append(parameter.Name);
        }
        lambdaParameters.Length -= 2;
        methodSignature.Length -= 2;
        if (vectorized) {
            methodSignature.Append(", bool vectorized = true");
        }
        var blueprintMethod = query.BlueprintMethod;
        var methodName      = blueprintMethod.Name;
        var avxMethod       = query.CustomMethod ?? $"_{methodName}_Avx{query.Hash}";
        
        var shadowMethodSource = $@"
        /// <summary>Vector method generated for: <see cref=""{methodName}""/>.</summary>
        public {(blueprintMethod.IsStatic ? "static " : "")}void {methodName}Vector({methodSignature})
        {{
            int count = {query.VectorTypes[0].Parameter.Name}.Length;
            int n = 0;
            if (vectorized) {{
                if (Avx.IsSupported) {{
                    n = {avxMethod}({avxParameters});
                }}
            }}
            for (; n < count; n++) {{
                {methodName}({lambdaParameters});
            }}
        }}";
        return shadowMethodSource;
    }
}
