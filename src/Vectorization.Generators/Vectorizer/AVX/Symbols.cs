// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.Generators.AVX;

public sealed partial class AvxVectorizer
{
    public ComputeResult Compute_MemberAccess(StringBuilder[] lanes, Query query, MemberAccessExpressionSyntax memberAccess)
    {
        var memberExpression = memberAccess.Expression;
        /* if (memberExpression is MemberAccessExpressionSyntax childMemberAccess) {
        	// Required to for: Vector3.Length()
            return Compute_MemberAccess(lanes, query, childMemberAccess);
        } */
        if (memberExpression is not IdentifierNameSyntax identifierNameSyntax) {
            return ComputeResult.Invalid;
        }
        var symbolInfo = query.SemanticModel.GetSymbolInfo(memberAccess);
        var symbol = symbolInfo.Symbol;
        var shape = Vectorizer.GetShapeFromExpression(query, memberAccess);
        var isStatic = symbol != null && symbol.IsStatic;
        if (isStatic)
        {
            // var value = symbol!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var value = $"{symbol.ContainingType.ToDisplayString()}.{symbol.Name}"; 
            var name = query.AddConst();
            /* if (symbol is IPropertySymbol typeSymbol) {
                var paramType = typeSymbol.Type.SpecialType == SpecialType.System_Single ? ParamType.Scalar : ParamType.Vector;
                query.paramTypes.Add(name, paramType);
            } */ 
            query.locals.AppendLine($"            var {name} = {value}; // static");
            var isScalar = VectorUtils.InterleaveVector3(query.locals, name, query);
            query.AddParam(name, false, isScalar, false, 0);
            query.locals.AppendLine();
            
            for (int n = 0; n < lanes.Length; n++) {
                var vectorName = query.GetVectorName(name, n);
                lanes[n].Append(vectorName);
            }
        } else {
            var name = identifierNameSyntax.Identifier.Text;
            query.readVectors.Add(name);
            if (query.paramTypes.TryGetValue(name, out var paramType)) { // SOA
                if (paramType.dimension == 1 && query.vectorDimension > 1) {
                    query.requireDeinterleave = true;
                }
            }
            for (int i = 0; i < lanes.Length; i++) {
                var vectorName = query.GetVectorName(name, i);
                lanes[i].Append(vectorName);
            }
        }
        return shape;
    }
    
    public ComputeResult Compute_IdentifierName(StringBuilder[] lanes, Query query, IdentifierNameSyntax identifierName)
    {
        var name = identifierName.Identifier.Text;
        for (int i = 0; i < lanes.Length; i++) {
            var vectorName = query.GetVectorName(name, i);
            lanes[i].Append(vectorName);
        }
        return Vectorizer.GetShapeFromExpression(query, identifierName);
    }
    

    public string? GetMethodName(Query query, InvocationExpressionSyntax invocation)
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

    public ComputeResult Compute_Literal(StringBuilder[] lanes, Query query, LiteralExpressionSyntax literal)
    {
        var name = query.AddConst();
        query.locals.AppendLine($"            var {name}_scalar = Vector256.Create<float>({literal.Token.Text}); // literal");
        query.locals.AppendLine();
        for (int n = 0; n < lanes.Length; n++) {
            lanes[n].Append($"{name}_scalar");
        }
        return ComputeResult.Scalar;
    }
}