// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.Generators;

public static class Vectorizer
{
    public static ComputeResult Compute_IdentifierName(StringBuilder[] lanes, Query query, IdentifierNameSyntax identifierName)
    {
        var name = identifierName.Identifier.Text;
        for (int i = 0; i < lanes.Length; i++) {
            var vectorName = query.GetVectorName(name, i);
            lanes[i].Append(vectorName);
        }
        return Vectorizer.GetShapeFromExpression(query, identifierName);
    }
    

    public static string? GetMethodName(Query query, InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            var symbolInfo = query.SemanticModel.GetSymbolInfo(memberAccess);
            if (symbolInfo.Symbol is IMethodSymbol methodSymbol) {
                return methodSymbol.ToDisplayString();
            }
        }
        return null;
    }

    public static  IdentifierNameSyntax GetMemberName(ExpressionSyntax expressionSyntax)
    {
        if (expressionSyntax is MemberAccessExpressionSyntax leftExpressionSyntax) {
            return leftExpressionSyntax.Expression as IdentifierNameSyntax;
        }
        if (expressionSyntax is IdentifierNameSyntax identifierNameSyntax) {
            return identifierNameSyntax;
        }
        return null;
    }
    
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