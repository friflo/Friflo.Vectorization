// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Text;
using Friflo.Vectorization.Generators;
using Microsoft.CodeAnalysis;

// ReSharper disable once CheckNamespace
// Note: Used small namespace and class name to enable shorter path names in 'Generated' folders
namespace Friflo;

internal static class VectorGenerator
{
    internal static string EmitVectorSource(Query query)
    {
        var vectorTypes = query.VectorTypes;
        var vectorized  = query.vectorized;
        var lambdaParameters    = new StringBuilder();
        var methodSignature     = new StringBuilder();
        var avxParameters       = new StringBuilder();
        avxParameters.Append("count");
        
        foreach (var vectorType in vectorTypes)
        {
            var type        = vectorType.FullQualifiedName;
            var name        = vectorType.Name;
            GeneratorUtils.AppendRefKind(lambdaParameters, vectorType.RefKind);
            avxParameters.Append(", ");
            if (vectorType.IsSpan) {
                lambdaParameters.Append($"{name}[n], ");
                var span = vectorType.RefKind == RefKind.Ref ? "Span" : "ReadOnlySpan";
                methodSignature.Append($"{span}<{type}> {name}, ");
                avxParameters.Append(name);
                continue;
            }
            lambdaParameters.Append($"{name}, ");
            
            GeneratorUtils.AppendRefKind(methodSignature, vectorType.RefKind);
            methodSignature.Append($"{type} {name}, ");
            
            GeneratorUtils.AppendRefKind(avxParameters, vectorType.RefKind);
            avxParameters.Append(name);
        }
        lambdaParameters.Length -= 2;
        methodSignature.Length -= 2;
        methodSignature.Append(", bool vectorized = true");
        var blueprintMethod = query.BlueprintMethod;
        var methodName      = blueprintMethod.Name;
        
        if (vectorized)
        {
            var avxMethod       = query.CustomMethod ?? $"_{methodName}_Avx{query.Hash}";
            
            // language=csharp
            var shadowMethodSource =
            $$""""

                    /// <summary>Vector method generated for: <see cref="{{methodName}}"/>.</summary>
                    public {{(blueprintMethod.IsStatic ? "static " : "")}}void {{methodName}}Vector({{methodSignature}})
                    {
                        int count = {{query.VectorTypes[0].Name}}.Length;
                        int n = 0;
                        if (vectorized) {
                            if (Avx.IsSupported) {
                                n = {{avxMethod}}({{avxParameters}});
                            }
                        }
                        for (; n < count; n++) {
                            {{methodName}}({{lambdaParameters}});
                        }
                    }
            """";
            return shadowMethodSource;
        }
        // language=csharp
        var source =
        $$""""

                /// <summary>Vector method generated for: <see cref="{{methodName}}"/>.</summary>
                public {{(blueprintMethod.IsStatic ? "static " : "")}}void {{methodName}}Vector({{methodSignature}})
                {
                    int count = {{query.VectorTypes[0].Name}}.Length;
                    for (int n = 0; n < count; n++) {
                        {{methodName}}({{lambdaParameters}});
                    }
                }
        """";
        return source;
    }
}
