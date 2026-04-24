// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Friflo.Vectorization.Generators;

public enum ParamType
{
    None,
    Scalar,
    Vector,
    Matrix4x4
}

public enum VectorLayout : byte
{
    /// <summary> Interleaved: [X, Y, Z, X, Y, Z] - Best for random access. </summary>
    AoS = 0,
    /// <summary> Parallel: [X, X...], [Y, Y...], [Z, Z...] - Best for SIMD. </summary>
    SoA = 1
}

public class VectorType
{
    public required string           Name               { get; init; }
    public required IParameterSymbol Parameter          { get; init; }
    public required string           FullQualifiedName  { get; init; }
    public required bool             IsSpan             { get; init; }
    public required bool             IsScalar           { get; init; }
    public required ITypeSymbol      ValueType          { get; init; }
    public required SpecialType      ValueSpecialType   { get; init; }
    public required ParamType        ParamType          { get; init; }
    public required int              Dimension          { get; init; }
    public required VectorLayout     Layout             { get; init; }

    public override string ToString() {
        return $"{Parameter} : {ValueType.Name} ({(ParamType == ParamType.Vector ? "vector" : "scalar")})";
    }
    
    public static VectorType[] GetVectorTypes(Diagnostics diagnostics, BlueprintParameter[] parameters)
    {
        var vectorTypes = new VectorType[parameters.Length];
        for (int n = 0; n < parameters.Length; n++) {
            var vectorType = parameters[n].VectorType;
            if (vectorType == null) {
                var symbol = parameters[n].Symbol;
                diagnostics.ReportDiagnosticSymbol(Errors.InvalidComponentType, symbol, symbol.Type.Name, symbol.Name);
                return [];
            }
            vectorTypes[n] = vectorType;
        }
        return vectorTypes;
    }
    
    public static VectorType? GetComponentVectorType(IParameterSymbol symbol, bool isComponent)
    {
        var type = symbol.Type;
        var typeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (!isComponent) {
            return CreateVectorType(symbol, typeName, false, symbol.Type, VectorLayout.AoS);
        }
        IFieldSymbol? valueField = null;
        var fields = type.GetMembers().OfType<IFieldSymbol>();
        foreach (var field in fields) {
            if (field.Name == "value" || field.Name == "Value") {
                valueField = field;
                break;
            }
        }
        if (valueField == null) {
            return null;
        }
        var layout = Utils.HasAttribute(type.GetAttributes(), "Friflo.Engine.ECS.AoSoAAttribute") ? 
                        VectorLayout.SoA : VectorLayout.AoS;
        return CreateVectorType(symbol, typeName, true, valueField.Type, layout);
    }
    
    public static VectorType GetSpanVectorType(IParameterSymbol symbol, bool isSpan)
    {
        var type        = symbol.Type;
        var typeName    = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var vectorType  = CreateVectorType(symbol, typeName, isSpan, symbol.Type, VectorLayout.AoS);
        return vectorType;
    }
    
    public static (SpecialType specialType, int dimension, ParamType paramType)
        GetTypeDim(ITypeSymbol? valueType)
    {
        if (valueType == null) {
            return (SpecialType.None, 0,  ParamType.None);
        }
        var specialType = valueType.SpecialType;
        switch (specialType) {
            case SpecialType.None:
                var fullTypeName = valueType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                switch (fullTypeName)
                {
                    case "global::System.Numerics.Vector2":     return (SpecialType.System_Single, 2,  ParamType.Vector);
                    case "global::System.Numerics.Vector3":     return (SpecialType.System_Single, 3,  ParamType.Vector);
                    case "global::System.Numerics.Vector4":     return (SpecialType.System_Single, 4,  ParamType.Vector);
                    case "global::System.Numerics.Matrix4x4":   return (SpecialType.System_Single, 0,  ParamType.Matrix4x4);
                }
                break;
            case SpecialType.System_Single:
                return (SpecialType.System_Single, 1,  ParamType.Scalar);
        }
        return (specialType, 0,  ParamType.None);
    }
    
    private static VectorType CreateVectorType(IParameterSymbol parameter, string fullQualifiedName, bool isSpan, ITypeSymbol valueType, VectorLayout layout)
    {
        bool isScalar   = !isSpan;
        var (specialType, dimension, paramType) = GetTypeDim(valueType);
        if (dimension == 3) {
            isScalar    = false;
        }
        return new VectorType {
            Name                = parameter.Name, 
            Parameter           = parameter,
            FullQualifiedName   = fullQualifiedName,
            IsSpan              = isSpan,
            IsScalar            = isScalar,  
            ValueType           = valueType,
            ValueSpecialType    = specialType, 
            ParamType           = paramType,
            Dimension           = dimension, 
            Layout              = layout
        };
    }
    
    public static int GetVectorTypeDimension(Query query, VectorType[] vectorTypes)
    {
        var dimension = 0;
        var success = true;
        IParameterSymbol? currentParameter = null;
        foreach (var vectorType in vectorTypes) {
            if (vectorType.ParamType == ParamType.None) {
                success = false;
                query.Diagnostics.ReportDiagnosticSymbol(Errors.InvalidParameterType, vectorType.Parameter, vectorType.Parameter.Type.Name);
            }
            if (!vectorType.IsSpan && vectorType.Dimension == 1) {
                continue;
            }
            if (dimension == 0 || dimension == 1) {
                dimension = vectorType.Dimension;
                currentParameter = vectorType.Parameter;
                continue;
            }
            if (vectorType.Dimension > 1 && vectorType.Dimension != dimension) {
                query.Diagnostics.ReportDiagnosticSymbol(Errors.IncompatibleParameterTypes, null, currentParameter?.Type.Name, vectorType.Parameter.Type.Name);
                success = false;
                continue;
            }
        }
        return success ? dimension : 0;
    }
}

