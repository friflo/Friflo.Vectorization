// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.Generators.WGSL;

public sealed partial class WgslVectorizer
{
    public StringBuilder[] CreateLanes(Query query, ISymbol? symbol, string parameterName)
    {
        var laneCount = 1;
        var lanes = query.lanes = new StringBuilder[laneCount];
        lanes[0] = new StringBuilder();
        return lanes;
    }
    
    public ComputeResult Compute_Assignment(StringBuilder[] lanes, Query query, AssignmentExpressionSyntax assignment)
    {
        var kind = assignment.Kind();
        var wgslOp = kind switch {
            SyntaxKind.SimpleAssignmentExpression   => "=",
            SyntaxKind.AddAssignmentExpression      => "+=",
            SyntaxKind.SubtractAssignmentExpression => "-=",
            SyntaxKind.MultiplyAssignmentExpression => "*=",
            SyntaxKind.DivideAssignmentExpression   => "/=",
            _                                       => null
        };
        if (wgslOp is null) {
            query.Diagnostics.ReportDiagnosticSyntax(Errors.OperationUnsupported, assignment);
            return ComputeResult.Invalid;
        }
        var leftIdentifier  = Symbols.GetMemberName(assignment.Left).Identifier.Text;
		var leftSymbol = query.SemanticModel.GetSymbolInfo(assignment.Left).Symbol;
        var leftShape       = Symbols.GetShapeFromExpression(query, assignment.Left);
        lanes = CreateLanes(query, leftSymbol, leftIdentifier);
        
        // read vector for all cases except "="
        if (kind != SyntaxKind.SimpleAssignmentExpression) {
            query.readVectors.Add(leftIdentifier);
        }
        lanes[0].Append($"_{leftIdentifier} {wgslOp} ");

        if (!Compute(lanes, query, assignment.Right)) {
            return ComputeResult.Invalid;
        }
        lanes[0].Append(";");        
        query.AddDirty(leftIdentifier);
        return leftShape;
    }

    public ComputeResult Compute_Binary(StringBuilder[] lanes, Query query, BinaryExpressionSyntax binary)
    {
        var kind = binary.Kind();
        var wgslOp = kind switch {
            SyntaxKind.AddExpression      => "+",
            SyntaxKind.SubtractExpression => "-",
            SyntaxKind.MultiplyExpression => "*",
            SyntaxKind.DivideExpression   => "/",
            _                             => null
        };
        if (wgslOp is null) {
            query.Diagnostics.ReportDiagnosticSyntax(Errors.OperationUnsupported, binary);
            return ComputeResult.Invalid;
        }
        var shape = Symbols.GetShapeFromExpression(query, binary);

        // Special case optimization: x / Sqrt(y) -> x * inverseSqrt(y)
        if (kind == SyntaxKind.DivideExpression && 
            binary.Right is InvocationExpressionSyntax rightInv &&
            Symbols.GetMethodName(query, rightInv) == "System.MathF.Sqrt(float)")
        {
            lanes[0].Append("(");
            if (!Compute(lanes, query, binary.Left)) return ComputeResult.Invalid;
            
            lanes[0].Append(" * inverseSqrt(");
            
            var sqrtArg = rightInv.ArgumentList.Arguments[0].Expression;
            if (!Compute(lanes, query, sqrtArg)) return ComputeResult.Invalid;
            
            lanes[0].Append("))");
            return shape;
        }
        // default case:
        lanes[0].Append("(");
        if (!Compute(lanes, query, binary.Left)) return ComputeResult.Invalid;
        
        lanes[0].Append($" {wgslOp} ");
        
        if (!Compute(lanes, query, binary.Right)) return ComputeResult.Invalid;
        lanes[0].Append(")");
        return shape;
    }
}