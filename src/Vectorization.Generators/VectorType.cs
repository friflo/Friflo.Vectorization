// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
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
    public IParameterSymbol parameter;
    public string           fullQualifiedName;
    public bool             isSpan;
    public bool             isScalar;
    public ITypeSymbol      valueType;
    public SpecialType      valueSpecialType;
    public ParamType        paramType;
    public int              dimension;
    public VectorLayout     layout;

    public override string ToString() {
        return $"{parameter} : {valueType.Name} ({(paramType == ParamType.Vector ? "vector" : "scalar")})";
    }
    
    
    public static VectorType[]? GetVectorTypes(Query query)
    {
        var result = new List<VectorType>();
        foreach (var parameter in query.Parameters)
        {
            var type = parameter.Type;
            var typeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            bool isSpan = query.VectorMode == VectorMode.Query && query.NamedTypes.IsComponent(type);
            if (isSpan) {
                IFieldSymbol? valueField = null;
                foreach (var field in type.GetMembers().OfType<IFieldSymbol>()) {
                    if (field.Name == "value" || field.Name == "Value") {
                        valueField = field;
                        break;
                    }
                }
                if (valueField == null) {
                    query.ReportDiagnosticSymbol(Errors.InvalidComponentType, parameter, type.Name, parameter.Name);
                    return null;
                }
                var layout = Utils.HasAttribute(type.GetAttributes(), "Friflo.Engine.ECS.SoAAttribute") ? 
                                VectorLayout.SoA : VectorLayout.AoS;
                var vectorType = CreateVectorType(parameter, typeName, true, valueField.Type, layout);
                result.Add(vectorType);
            } else {
                isSpan = query.VectorMode == VectorMode.Vector &&
                         Utils.HasAttribute(parameter.GetAttributes(), "Friflo.Vectorization.SpanAttribute");
                var vectorType = CreateVectorType(parameter, typeName, isSpan, parameter.Type, VectorLayout.AoS);
                result.Add(vectorType);
            }
        }
        return result.ToArray();
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
                    case "global::System.Numerics.Matrix4x4":   return (SpecialType.System_Single, 4,  ParamType.Matrix4x4);
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
            parameter           = parameter,
            fullQualifiedName   = fullQualifiedName,
            isSpan              = isSpan,
            isScalar            = isScalar,  
            valueType           = valueType,
            valueSpecialType    = specialType, 
            paramType           = paramType,
            dimension           = dimension, 
            layout              = layout
        };
    }
    
    public static int GetVectorTypeDimension(Query query, VectorType[] vectorTypes)
    {
        var dimension = 0;
        var success = true;
        IParameterSymbol? currentParameter = null;
        foreach (var vectorType in vectorTypes) {
            if (vectorType.paramType == ParamType.None) {
                success = false;
                query.ReportDiagnosticSymbol(Errors.InvalidParameterType, vectorType.parameter, vectorType.parameter.Type.Name);
            }
            if (!vectorType.isSpan && vectorType.dimension == 1) {
                continue;
            }
            if (dimension == 0 || dimension == 1) {
                dimension = vectorType.dimension;
                currentParameter = vectorType.parameter;
                continue;
            }
            if (vectorType.dimension > 1 && vectorType.dimension != dimension) {
                query.ReportDiagnosticSymbol(Errors.IncompatibleParameterTypes, null, currentParameter?.Type.Name, vectorType.parameter.Type.Name);
                success = false;
                continue;
            }
        }
        return success ? dimension : 0;
    }
}

