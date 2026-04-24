using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Friflo.Vectorization.Generators;

public class BlueprintParameter
{
    public required IParameterSymbol    Symbol      { get; init; }
    public required VectorType?         VectorType  { get; init; }
    public required bool                IsSpan      { get; init; }
    public required bool                IsEntity    { get; init; }

    public override string ToString() => Symbol.Name;
    
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
            switch (vectorMode) {
                case VectorMode.Query:
                    isSpan      = IsComponent(parameter.Type, componentInterface!);
                    vectorType  = VectorType.GetComponentVectorType(parameter, isSpan);
                    break;
                case VectorMode.Vector:
                    isSpan      = Utils.HasAttribute(parameter.GetAttributes(), "Friflo.Vectorization.SpanAttribute");
                    vectorType  = VectorType.GetSpanVectorType(parameter, isSpan);
                    break;
            }
            bool isEntity = !isSpan && IsEntityParameter(parameter, entityStruct!);
            blueprintParam[n] = new BlueprintParameter{ Symbol = parameter, VectorType = vectorType, IsSpan = isSpan, IsEntity = isEntity };
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
