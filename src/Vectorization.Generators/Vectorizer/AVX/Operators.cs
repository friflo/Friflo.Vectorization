// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.Generators.AVX;

public partial class AvxVectorizer
{
    public StringBuilder[] CreateLanes(Query query, ISymbol? symbol, string parameterName)
    {
        var laneCount = query.laneCount;
        ITypeSymbol? typeSymbol = null;
        if (symbol is ILocalSymbol localSymbol) {
            typeSymbol = localSymbol.Type;
        }
        if (symbol is IFieldSymbol fieldSymbol) {
            typeSymbol = fieldSymbol.Type;
        }
        // SOA
        var (_, dimension, _) = VectorType.GetTypeDim(typeSymbol);
        if (query.useDeinterleave && !query.paramTypes.ContainsKey(parameterName)) {
            query.AddParam(parameterName, false, true, false, dimension);    
        }
        if (query.useDeinterleave && dimension == 1) {
            laneCount = 2;
        }
        if (query.paramTypes.TryGetValue(parameterName, out var paramType)) {
            if (paramType.dimension == 1) {
                laneCount =  query.vectorDimension switch {
                    2 => 2,
                    3 => 1,
                    4 => 1,
                    _ => laneCount
                };
            }
        }
        var lanes = query.lanes = new StringBuilder[laneCount];
        for (int n = 0; n < laneCount; n++) {
            lanes[n] = new StringBuilder();
        }
        return lanes;
    }
    
    public ComputeResult Compute_Assignment(StringBuilder[] lanes, Query query, AssignmentExpressionSyntax assignment)
    {
        var kind = assignment.Kind();
        var avxOperation = kind switch
        {
            SyntaxKind.SimpleAssignmentExpression   => "",              // =
            SyntaxKind.AddAssignmentExpression      => "Avx.Add",       // +=
            SyntaxKind.SubtractAssignmentExpression => "Avx.Subtract",  // -=
            SyntaxKind.MultiplyAssignmentExpression => "Avx.Multiply",  // *=
            SyntaxKind.DivideAssignmentExpression   => "Avx.Divide",    // /=
            _                                       => null
        };
        if (avxOperation is null) {
            query.Diagnostics.ReportDiagnosticSyntax(Errors.OperationUnsupported, assignment);
            return ComputeResult.Invalid;
        }
        var leftIdentifier = VectorUtils.GetMemberName(assignment.Left).Identifier;
        var left = leftIdentifier.Text;
        if (kind != SyntaxKind.SimpleAssignmentExpression) {
            query.readVectors.Add(left);  // e.g. += -=
        }
        var leftSymbol = query.SemanticModel.GetSymbolInfo(assignment.Left).Symbol;
        var leftShape = Vectorizer.GetShapeFromExpression(query, assignment.Left);
        lanes = CreateLanes(query, leftSymbol, left);
        // FMA is a "Cheat Code" for:    (vel * dt) + pos    ->    Fma.MultiplyAdd(vel, dt, pos);
        if (kind == SyntaxKind.AddAssignmentExpression && 
            assignment.Right is BinaryExpressionSyntax assignBinary && assignBinary.Kind() is SyntaxKind.MultiplyExpression)
        {
            for (int i = 0; i < lanes.Length; i++) {
                var vectorName = query.GetVectorName(left, i);
                lanes[i].Append($"{vectorName} = Fma.MultiplyAdd(");
            }
            if (!Compute(lanes, query, assignBinary.Left)) {
                return ComputeResult.Invalid;
            }
            lanes.Append(", ");
            if (!Compute(lanes, query, assignBinary.Right)) {
                return ComputeResult.Invalid;
            }
            for (int i = 0; i < lanes.Length; i++) {
                var vectorName = query.GetVectorName(left, i);
                lanes[i].Append($", {vectorName});");
            }
            query.AddDirty(left);
            return leftShape;
        }
        for (int i = 0; i < lanes.Length; i++) {
            var vectorName = query.GetVectorName(left, i);
            if (kind == SyntaxKind.SimpleAssignmentExpression) {
                lanes[i].Append($"{vectorName} = ");
            } else {
                lanes[i].Append($"{vectorName} = {avxOperation}({vectorName}, ");
            }
        }
        if (!Compute(lanes, query, assignment.Right)) {
            return ComputeResult.Invalid;
        }
        lanes.Append(kind == SyntaxKind.SimpleAssignmentExpression ? ";" : ");");
        query.AddDirty(left);
        return leftShape;
    }

    public ComputeResult Compute_Binary(StringBuilder[] lanes, Query query, BinaryExpressionSyntax binary)
    {
        var kind = binary.Kind();
        var avxOperation = kind switch
        {
            SyntaxKind.AddExpression      => "Add",         // +
            SyntaxKind.SubtractExpression => "Subtract",    // -
            SyntaxKind.MultiplyExpression => "Multiply",    // *
            SyntaxKind.DivideExpression   => "Divide",      // /
            _                             => null
        };
        if (avxOperation is null) {
            query.Diagnostics.ReportDiagnosticSyntax(Errors.OperationUnsupported, binary);
            return ComputeResult.Invalid;
        }
        var shape = Vectorizer.GetShapeFromExpression(query, binary);

        // is reciprocal square root:     left / Sqrt(right) 
        if (kind == SyntaxKind.DivideExpression) {
            if (binary.Right is InvocationExpressionSyntax rightInvocation &&
                GetMethodName(query, rightInvocation) == "System.MathF.Sqrt(float)")
            {
                lanes.Append("Avx.Multiply(Avx.ReciprocalSqrt(");
                if (!Compute(lanes, query, rightInvocation.ArgumentList.Arguments[0].Expression)) {
                    return ComputeResult.Invalid;
                }
                lanes.Append("), ");
                if (!Compute(lanes, query, binary.Left)) {
                    return ComputeResult.Invalid;
                }
                lanes.Append(")");
                return DataShape.Scalar;
            }
        }
        // FMA is a "Cheat Code" for:    (vel * dt) + pos    ->    Fma.MultiplyAdd(vel, dt, pos);
        if (kind == SyntaxKind.AddExpression && 
            binary.Left is BinaryExpressionSyntax multiplyBinary && multiplyBinary.Kind() is SyntaxKind.MultiplyExpression)
        {
            lanes.Append("Fma.MultiplyAdd(");
            if (!Compute(lanes, query, multiplyBinary.Left)) {
                return ComputeResult.Invalid;
            }
            lanes.Append(", ");
            if (!Compute(lanes, query, multiplyBinary.Right)) {
                return ComputeResult.Invalid;
            }
            lanes.Append(", ");
            if (!Compute(lanes, query, binary.Right)) {
                return ComputeResult.Invalid;
            }
            lanes.Append(")");
            return shape;
        }
        for (int i = 0; i < lanes.Length; i++) {
            lanes[i].Append($"Avx.{avxOperation}(");
        }
        if (!Compute(lanes, query, binary.Left)) {
            return ComputeResult.Invalid;
        }
        lanes.Append(", ");
        if (!Compute(lanes, query, binary.Right)) {
            return ComputeResult.Invalid;
        }
        lanes.Append(")");
        return shape;
    }
}