// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.Generators;

public sealed class BlueprintParameter
{
    public required string              Name        { get; init; } // = Symbol.Name
    public required IParameterSymbol    Symbol      { get; init; }
    public required string              TypeName    { get; init; }
    public required VectorType?         VectorType  { get; init; }
    public required bool                IsSpan      { get; init; }
    public required bool                IsEntity    { get; init; }

    public override string ToString() => Name;
    
    private static bool IsComponent(ITypeSymbol typeSymbol, INamedTypeSymbol componentInterface) {
        return typeSymbol.AllInterfaces.Contains(componentInterface);
    }
    
    private static bool IsEntityParameter(IParameterSymbol parameter, INamedTypeSymbol entityStruct) {
        return parameter.Name == "entity" && SymbolEqualityComparer.Default.Equals(parameter.Type, entityStruct);
    }
    
    public static BlueprintParameter[] CreateBlueprintParameters(
        ImmutableArray<IParameterSymbol>    parameters,
        VectorMode                          vectorMode,
        Compilation                         compilation)
    {
        var componentInterface  = compilation.GetTypeByMetadataName("Friflo.Engine.ECS.IComponent");
        var entityStruct        = compilation.GetTypeByMetadataName("Friflo.Engine.ECS.Entity");
        
        var blueprintParam = new BlueprintParameter[parameters.Length];
        for (int n = 0; n < parameters.Length; n++)
        {
            var parameter = parameters[n];
            VectorType? vectorType  = null;
            bool        isSpan      = false;
            var typeName = parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (typeName.StartsWith("global::System.Numerics.")){
                typeName = typeName.Substring("global::System.Numerics.".Length);
            }
            switch (vectorMode) {
                case VectorMode.Query:
                    isSpan      = IsComponent(parameter.Type, componentInterface!);
                    vectorType  = VectorType.GetComponentVectorType(parameter, typeName, isSpan);
                    break;
                case VectorMode.Vector:
                    isSpan      = GeneratorUtils.HasAttribute(parameter.GetAttributes(), "Friflo.Vectorization.SpanAttribute");
                    vectorType  = VectorType.GetSpanVectorType(parameter, typeName, isSpan);
                    break;
            }
            bool isEntity = !isSpan && IsEntityParameter(parameter, entityStruct!);
            blueprintParam[n] = new BlueprintParameter {
                Name        = parameter.Name,
                Symbol      = parameter,
                TypeName    = typeName,
                VectorType  = vectorType,
                IsSpan      = isSpan,
                IsEntity    = isEntity
            };
        }
        return blueprintParam;
    }
    
    public static BlueprintParameter[] GetVectorSpans(BlueprintParameter[] parameters)
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
}
