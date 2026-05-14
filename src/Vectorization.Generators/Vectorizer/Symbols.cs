// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.Generators;

public static class Vectorizer
{
    internal static DataShape GetShapeFromExpression(Query query, ExpressionSyntax expression) 
    {
        var typeInfo = query.SemanticModel.GetTypeInfo(expression);
        var type = typeInfo.Type;
        if (type != null) {
            return GetSystemShape(type);
        }
        return DataShape.None;
    }
    
    private static DataShape GetSystemShape(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_Single) {
            return DataShape.Scalar;
        }
        string name = type.ToDisplayString();
        return name switch {
            "System.Numerics.Vector2" => DataShape.Vector,
            "System.Numerics.Vector3" => DataShape.Vector,
            "System.Numerics.Vector4" => DataShape.Vector,
            _ => DataShape.None
        };
    }
}