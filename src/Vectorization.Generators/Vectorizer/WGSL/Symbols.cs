// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// ReSharper disable MergeIntoPattern
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.Generators.WGSL;

public sealed partial class WgslVectorizer
{
    public ComputeResult Compute_MemberAccess(StringBuilder[] lanes, Query query, MemberAccessExpressionSyntax memberAccess)
    {
        var memberExpression = memberAccess.Expression;
        if (memberExpression is not IdentifierNameSyntax identifierNameSyntax) {
            return ComputeResult.Invalid;
        }
        var symbolInfo  = query.SemanticModel.GetSymbolInfo(memberAccess);
        var symbol      = symbolInfo.Symbol;
        var shape       = Symbols.GetShapeFromExpression(query, memberAccess);
        var isStatic    = symbol != null && symbol.IsStatic;
        if (isStatic) {
            // Try reading constant value - e.g. MathF.PI
            if (symbol is IFieldSymbol field && field.HasConstantValue) { 
                object val = field.ConstantValue;
                string literal = val switch {
                    float f  => f.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    _        => val?.ToString() ?? "0.0"
                };
                if (!literal.Contains(".")) literal += ".0";  // ensure type safety for floats
                lanes[0].Append(literal);
            } else  {
                // fallback for non const fields / properties
                var name = query.AddConst();
                var value = $"{symbol.ContainingType.ToDisplayString()}.{symbol.Name}"; 
                
                query.locals.AppendLine($"            var {name} = {value}; // static");
                query.AddParam(name, false, shape == DataShape.Scalar, false, 0);
                query.locals.AppendLine();
                lanes[0].Append(name);
            }
        } else {
            var name = identifierNameSyntax.Identifier.Text;
            query.readVectors.Add(name);
            lanes[0].Append(name);
        }
        return shape;
    }
    
    public ComputeResult Compute_Literal(StringBuilder[] lanes, Query query, LiteralExpressionSyntax literal)
    {
        var value = literal.Token.Text;
        if (!value.Contains(".") && !value.Contains("e") && !value.Contains("f")) {
            value += ".0";
        }
        value = value.Replace("f", "");
        lanes[0].Append(value);
        return ComputeResult.Scalar;
    }
}