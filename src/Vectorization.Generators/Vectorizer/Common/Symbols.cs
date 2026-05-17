// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.Generators;

public static class Symbols
{
    public static ComputeResult Compute_IdentifierName(StringBuilder[] lanes, Query query, IdentifierNameSyntax identifierName)
    {
        var name = identifierName.Identifier.Text;
        query.readVectors.Add(name); 	// Compute method is only called for read. Not for assignment
        for (int i = 0; i < lanes.Length; i++) {
            var vectorName = query.GetVectorName(name, i);
            lanes[i].Append(vectorName);
        }
        return GetShapeFromExpression(query, identifierName);
    }
    
    public static string? GetMethodName(Query query, InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess) {
            return GetSymbolName(query, memberAccess);
        }
        if (invocation.Expression is IdentifierNameSyntax identifierName) {
            return GetSymbolName(query, identifierName);
        }
        return null;
    }
    
    private static string? GetSymbolName(Query query, ExpressionSyntax syntax)
    {
        var symbolInfo = query.SemanticModel.GetSymbolInfo(syntax);
        if (symbolInfo.Symbol is IMethodSymbol methodSymbol) {
            return methodSymbol.ToDisplayString();
        }
        // fallback e.g. in case of System.MathF.Sign
        //      symbolInfo.CandidateReason == CandidateReason.OverloadResolutionFailure
        if (symbolInfo.CandidateSymbols.Length > 0 && symbolInfo.CandidateSymbols[0] is IMethodSymbol candidateMethod) {
            return candidateMethod.ToDisplayString();  // return System.MathF.Sign(float)
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
        if (type.SpecialType == SpecialType.System_Single ||
            type.SpecialType == SpecialType.System_Int32)
        {
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
    
    public static void Append(this StringBuilder[] sb, string text)
    {
        for (int n = 0; n < sb.Length; n++) {
            sb[n].Append(text);
        }
    }
}