// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.Generators.AVX;

public sealed partial class AvxVectorizer
{
    public ComputeResult Compute_Invocation(StringBuilder[] lanes, Query query, InvocationExpressionSyntax invocation)
    {
        var methodName = Vectorizer.GetMethodName(query, invocation);
        var methodReduced = methodName?.Replace("System.Numerics.Vector2", "Vector")
                                       .Replace("System.Numerics.Vector3", "Vector")
                                       .Replace("System.Numerics.Vector4", "Vector");
        var argList = invocation.ArgumentList;
        switch (methodReduced)
        {
            case "System.MathF.Sin(float)":         return Method_Scalar    (lanes, query, argList, "MathUtils.SinMathF");
            case "System.MathF.Cos(float)":         return Method_Scalar    (lanes, query, argList, "MathUtils.CosMathF");
            case "System.MathF.Tan(float)":         return Method_Scalar    (lanes, query, argList, "MathUtils.TanMathF");
            case "System.MathF.Asin(float)":        return Method_Scalar    (lanes, query, argList, "MathUtils.AsinMathF");
            case "System.MathF.Acos(float)":        return Method_Scalar    (lanes, query, argList, "MathUtils.AcosMathF");
            case "System.MathF.Atan(float)":        return Method_Scalar    (lanes, query, argList, "MathUtils.AtanMathF");
            case "System.MathF.Atan2(float, float)":return Method_Scalar    (lanes, query, argList, "MathUtils.Atan2MathF");
            case "System.MathF.Asinh(float)":       return Method_Scalar    (lanes, query, argList, "MathUtils.AsinhMathF");
            case "System.MathF.Acosh(float)":       return Method_Scalar    (lanes, query, argList, "MathUtils.AcoshMathF");
            case "System.MathF.Atanh(float)":       return Method_Scalar    (lanes, query, argList, "MathUtils.AtanhMathF");
            
            case "Vector.Abs(Vector)":              return Method_Abs       (lanes, query, argList, DataShape.Vector);
            case "System.MathF.Abs(float)":         return Method_Abs       (lanes, query, argList, DataShape.Scalar);
            case "System.MathF.Sign(float)":        return Method_Scalar    (lanes, query, argList, "MathUtils.SignMathF");
            case "Vector.Truncate(Vector)":         return Method_Truncate  (lanes, query, argList, DataShape.Vector);
            case "System.MathF.Truncate(float)":    return Method_Truncate  (lanes, query, argList, DataShape.Scalar);
            case "Vector.Round(Vector)":            return Method_Round     (lanes, query, argList, DataShape.Vector);
            case "System.MathF.Round(float)":       return Method_Round     (lanes, query, argList, DataShape.Scalar);
            case "System.MathF.Floor(float)":       return Method_Floor     (lanes, query, argList);
            case "System.MathF.Ceiling(float)":     return Method_Ceiling   (lanes, query, argList);
            
            case "System.MathF.Exp(float)":         return Method_Scalar    (lanes, query, argList, "Vector256.Exp");
            case "System.MathF.Log(float)":         return Method_Scalar    (lanes, query, argList, "Vector256.Log");
            case "System.MathF.Log10(float)":       return Method_Scalar    (lanes, query, argList, "MathUtils.Log10MathF");
            case "System.MathF.Log2(float)":        return Method_Scalar    (lanes, query, argList, "Vector256.Log2");
            case "System.MathF.Pow(float, float)":  return Method_Scalar    (lanes, query, argList, "MathUtils.PowMathF");
            case "System.MathF.Sqrt(float)":        return Method_Scalar    (lanes, query, argList, "Avx.Sqrt");
            
            case "System.MathF.Min(float, float)":          return Method_MinMax    (lanes, query, argList, DataShape.Scalar, "Min");
            case "Vector.Min(Vector, Vector)":              return Method_MinMax    (lanes, query, argList, DataShape.Vector, "Min");
            
            case "System.MathF.Max(float, float)":          return Method_MinMax    (lanes, query, argList, DataShape.Scalar, "Max");
            case "Vector.Max(Vector, Vector)":              return Method_MinMax    (lanes, query, argList, DataShape.Vector, "Max");
            
            case "System.Math.Clamp(float, float, float)":  return Method_Clamp     (lanes, query, argList, DataShape.Scalar);
            case "Vector.Clamp(Vector, Vector, Vector)":    return Method_Clamp     (lanes, query, argList, DataShape.Vector);
            
            case "Vector.Lerp(Vector, Vector, float)":
            case "Vector.Lerp(Vector, Vector, Vector)":     return Method_Lerp      (lanes, query, argList);
            
            // --- methods require Deinterleave
            case "Vector.Cross(Vector, Vector)":            return Method_Cross     (lanes, query, argList);
            
            case "Vector.Normalize(Vector)":                return Method_Normalize (lanes, query, argList);
            
            case "Vector.Length()":                         return Method_Length    (lanes, query, invocation);
            
            case "Vector.Distance(Vector, Vector)":         return Method_Distance  (lanes, query, argList, "Distance");
            case "Vector.DistanceSquared(Vector, Vector)":  return Method_Distance  (lanes, query, argList, "DistanceSquared");
            
            case "Vector.Transform(Vector, System.Numerics.Matrix4x4)":
                return Method_Vector4_Transform(lanes, query, argList);
        }
        query.Diagnostics.ReportDiagnosticSyntax(Errors.OperationUnsupported, invocation, invocation.ToFullString());
        return ComputeResult.Invalid;
    }

    public ComputeResult Method_Vector4_Transform(StringBuilder[] lanes, Query query, ArgumentListSyntax argList)
    {
        var dim = query.vectorDimension;
        if (query.strategy == Strategy.VerticalAoS && dim == 3) {
            query.requireDeinterleave = true; // no way to handle a 12 byte AoS efficient in the fast path
            return ComputeResult.Vector;
        }
        /*if (query.vectorDimension != 4) {
            return ComputeResult.Invalid;
        } */
        var args = argList.Arguments;
        if (!Compute_AddTemp(query, args[0].Expression, $"Transform arg[0]", out var arg0, false)) {
            return ComputeResult.Invalid;
        }
        if (args[1].Expression is IdentifierNameSyntax identifierNameSyntax) {
            var m = identifierNameSyntax.Identifier.Text;
            if (query.strategy == Strategy.VerticalAoS) {
                lanes.Append($"AvxVector{dim}.TransformMatrixAoS(");
                for (int n = 0; n < lanes.Length; n++) {
                    if (dim == 2) {
                        lanes[n].Append($"{arg0}_{n}, {m}_0, {m}_1, {m}_3)");
                    } else {
                        lanes[n].Append($"{arg0}_{n}, {m}_0, {m}_1, {m}_2, {m}_3)");
                    }
                }
            } else {
                /* var result = query.AddTemp();
                query.computeTemp.AppendLine($"                    var ({result}_0, {result}_1, {result}_2, {result}_3) = AvxVector4.TransformMatrixSoA({arg0}_0, {arg0}_1, {arg0}_2, {arg0}_3, {m}_0, {m}_1, {m}_2, {m}_3);");
                lanes[0].Append($"{result}_0");
                lanes[1].Append($"{result}_1");
                lanes[2].Append($"{result}_2");
                lanes[3].Append($"{result}_3"); */

                lanes.Append($"AvxVector{dim}.TransformMatrixSoA(");
                if (dim==2) {
                    lanes[0].Append($"{arg0}_0, {arg0}_1, Vector256.Create({m}.M11), Vector256.Create({m}.M21), Vector256.Create({m}.M41))");
                    lanes[1].Append($"{arg0}_0, {arg0}_1, Vector256.Create({m}.M12), Vector256.Create({m}.M22), Vector256.Create({m}.M42))");
                    lanes[2].Append($"{arg0}_2, {arg0}_3, Vector256.Create({m}.M11), Vector256.Create({m}.M21), Vector256.Create({m}.M41))");
                    lanes[3].Append($"{arg0}_2, {arg0}_3, Vector256.Create({m}.M12), Vector256.Create({m}.M22), Vector256.Create({m}.M42))");
                } else {
                    for (int n = 0; n < lanes.Length; n++) {
                        var i = n + 1;
                        var vectors = dim switch {
                            3 => $"{arg0}_0, {arg0}_1, {arg0}_2",
                            4 => $"{arg0}_0, {arg0}_1, {arg0}_2, {arg0}_3"
                        };
                        lanes[n].Append($"{vectors}, Vector256.Create({m}.M1{i}), Vector256.Create({m}.M2{i}), Vector256.Create({m}.M3{i}), Vector256.Create({m}.M4{i}))");
                    }
                }
            }
        }
        return DataShape.Vector;
    }

    public ComputeResult Method_MinMax(StringBuilder[] lanes, Query query, ArgumentListSyntax argList, DataShape shape, string op)
    {
        var args = argList.Arguments;
        for (int n = 0; n < lanes.Length; n++) {
            lanes[n].Append($"Avx.{op}(");
        }
        if (!Compute(lanes, query, args[0].Expression)) {
            return ComputeResult.Invalid;
        }
        lanes.Append(", ");
        if (!Compute(lanes, query, args[1].Expression)) {
            return ComputeResult.Invalid;
        }
        lanes.Append(")");
        return shape;
    }
    
    public ComputeResult Method_Clamp(StringBuilder[] lanes, Query query, ArgumentListSyntax argList, DataShape shape)
    {
        var args = argList.Arguments;
        lanes.Append("Avx.Min(");
        if (!Compute(lanes, query, args[2].Expression)) {
            return ComputeResult.Invalid;
        }
        lanes.Append(", Avx.Max(");
        if (!Compute(lanes, query, args[1].Expression)) {
            return ComputeResult.Invalid;
        }
        lanes.Append(", ");
        if (!Compute(lanes, query, args[0].Expression)) {
            return ComputeResult.Invalid;
        }
        lanes.Append("))");
        return shape;
    }
    
    public ComputeResult Method_Lerp(StringBuilder[] lanes, Query query, ArgumentListSyntax argumentSyntax)
    {
        var args = argumentSyntax.Arguments;
        lanes.Append("Fma.MultiplyAdd(");
        if (!Compute(lanes, query, args[2].Expression)) {
            return ComputeResult.Invalid;
        }
        lanes.Append(", Avx.Subtract(");
        if (!Compute(lanes, query, args[1].Expression)) {
            return ComputeResult.Invalid;
        }
        lanes.Append(", ");
        if (!Compute(lanes, query, args[0].Expression)) {
            return ComputeResult.Invalid;
        }
        lanes.Append("), ");
        if (!Compute(lanes, query, args[0].Expression)) {
            return ComputeResult.Invalid;
        }
        lanes.Append(")");
        return DataShape.Vector;
    }

    public ComputeResult Method_Abs(StringBuilder[] lanes, Query query, ArgumentListSyntax argList, DataShape shape)
    {
        var name = query.AddConst();
        query.locals.AppendLine($"            var {name} = Vector256.Create(0x7FFFFFFF).AsSingle(); // Abs()");
        query.locals.AppendLine();
        lanes.Append("Avx.And(");
        var args = argList.Arguments;
        if (!Compute(lanes, query, args[0].Expression)) {
            return ComputeResult.Invalid;
        }
        for (int n = 0; n < lanes.Length; n++) {
            lanes[n].Append($", {name})");
        }
        return shape;
    }
    
    public ComputeResult Method_Truncate(StringBuilder[] lanes, Query query, ArgumentListSyntax argList, DataShape shape)
    {
        lanes.Append("Vector256.Truncate(");    // alternative: Avx.RoundToNearestInteger(v, 0x03 | 0x08);
        var args = argList.Arguments;
        if (!Compute(lanes, query, args[0].Expression)) {
            return ComputeResult.Invalid;
        }
        lanes.Append(")");
        return shape;
    }
    
    public ComputeResult Method_Floor(StringBuilder[] lanes, Query query, ArgumentListSyntax argList)
    {
        lanes.Append("Vector256.Floor(");       // alternative: Avx.RoundToNearestInteger(value, 0x01 | 0x08);
        var args = argList.Arguments;
        if (!Compute(lanes, query, args[0].Expression)) {
            return ComputeResult.Invalid;
        }
        lanes.Append(")");
        return DataShape.Scalar;
    }
    
    public ComputeResult Method_Ceiling(StringBuilder[] lanes, Query query, ArgumentListSyntax argList)
    {
        lanes.Append("Vector256.Ceiling(");     // alternative:  Avx.RoundToNearestInteger(value, 0x02 | 0x08);
        var args = argList.Arguments;
        if (!Compute(lanes, query, args[0].Expression)) {
            return ComputeResult.Invalid;
        }
        lanes.Append(")");
        return DataShape.Scalar;
    }
    
    public ComputeResult Method_Round(StringBuilder[] lanes, Query query, ArgumentListSyntax argList, DataShape shape)
    {
        lanes.Append("Vector256.Round(");       // alternative:  Avx.RoundToNearestInteger(value, 0x00 | 0x08);
        var args = argList.Arguments;
        if (!Compute(lanes, query, args[0].Expression)) {
            return ComputeResult.Invalid;
        }
        lanes.Append(")");
        return shape;
    }

    public ComputeResult Method_Scalar(StringBuilder[] lanes, Query query, ArgumentListSyntax argList, string method)
    {
        for (int n = 0; n < lanes.Length; n++) {
            lanes[n].Append($"{method}(");
        }
        var args = argList.Arguments;
        for (int i = 0; i < args.Count; i++)
        {
            if (i > 0) {
                lanes.Append(", ");
            }
            if (!Compute(lanes, query, args[i].Expression)) {
                return ComputeResult.Invalid;
            }
        }
        lanes.Append(")");
        return DataShape.Scalar;
    }
    
    public ComputeResult Method_Cross(StringBuilder[] lanes, Query query, ArgumentListSyntax argList)
    {
        query.requireDeinterleave = true;
        var args = argList.Arguments;
        if (!Compute_AddTemp(query, args[0].Expression, "Cross arg[0]", out var a, false)) {
            return ComputeResult.Invalid;
        }
        if (!Compute_AddTemp(query, args[1].Expression, "Cross arg[1]", out var b, false)) {
            return ComputeResult.Invalid;
        }
        if (query.vectorDimension == 2) {
            lanes[0].Append($"Fma.MultiplySubtract({a}_0, {b}_1, Avx.Multiply({a}_1, {b}_0))");
            lanes[1].Append($"Fma.MultiplySubtract({a}_2, {b}_3, Avx.Multiply({a}_3, {b}_2))");
        }
        if (query.vectorDimension == 3 || query.vectorDimension == 4) {
            lanes[0].Append($"Fma.MultiplySubtract({a}_1, {b}_2, Avx.Multiply({a}_2, {b}_1))");
            lanes[1].Append($"Fma.MultiplySubtract({a}_2, {b}_0, Avx.Multiply({a}_0, {b}_2))");
            lanes[2].Append($"Fma.MultiplySubtract({a}_0, {b}_1, Avx.Multiply({a}_1, {b}_0))");
            if (query.vectorDimension == 4) {
                lanes[3].Append($"Avx.Multiply({a}_3, {b}_3)");
            }
        }
        return DataShape.Vector;
    }
    
    public ComputeResult Method_Normalize(StringBuilder[] lanes, Query query, ArgumentListSyntax argList)
    {
        query.requireDeinterleave = true;
        var args = argList.Arguments;
        if (!Compute_AddTemp(query, args[0].Expression, "Normalize arg[0]", out var arg0, true)) {
            return ComputeResult.Invalid;
        }
        var result = query.AddTemp();
        switch (query.vectorDimension)
        {
            case 2:
                query.computeTemp.AppendLine($"                    var ({result}_0, {result}_1) = AvxVector2.Normalize({arg0}_0, {arg0}_1);");
                query.computeTemp.AppendLine($"                    var ({result}_2, {result}_3) = AvxVector2.Normalize({arg0}_2, {arg0}_3);");
                lanes[0].Append($"{result}_0");
                lanes[1].Append($"{result}_1");
                lanes[2].Append($"{result}_2");
                lanes[3].Append($"{result}_3");
                return DataShape.Vector;
            case 3:
                query.computeTemp.AppendLine($"                    var ({result}_0, {result}_1, {result}_2) = AvxVector3.Normalize({arg0}_0, {arg0}_1, {arg0}_2);");
                lanes[0].Append($"{result}_0");
                lanes[1].Append($"{result}_1");
                lanes[2].Append($"{result}_2");
                return DataShape.Vector;
            case 4:
                query.computeTemp.AppendLine($"                    var ({result}_0, {result}_1, {result}_2, {result}_3) = AvxVector4.Normalize({arg0}_0, {arg0}_1, {arg0}_2, {arg0}_3);");
                lanes[0].Append($"{result}_0");
                lanes[1].Append($"{result}_1");
                lanes[2].Append($"{result}_2");
                lanes[3].Append($"{result}_3");
                return DataShape.Vector;
        }
        return ComputeResult.Invalid;
    }
    
    public ComputeResult Method_Length(StringBuilder[] lanes, Query query, InvocationExpressionSyntax invocation)
    {
        query.requireDeinterleave = true;
        var expression = invocation.Expression;
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess) {
            expression = memberAccess.Expression;
        }
        if (!Compute_AddTemp(query, expression, "Length this", out var arg0, true)) {
            return ComputeResult.Invalid;
        }
        switch (query.vectorDimension)
        {
            case 2:
                lanes[0].Append($"AvxVector2.Length({arg0}_0, {arg0}_1)");
                lanes[1].Append($"AvxVector2.Length({arg0}_2, {arg0}_3)");
                return DataShape.Scalar;
            case 3:
                lanes[0].Append($"AvxVector3.Length({arg0}_0, {arg0}_1, {arg0}_2)");
                return DataShape.Scalar;
            case 4:
                lanes[0].Append($"AvxVector4.Length({arg0}_0, {arg0}_1, {arg0}_2, {arg0}_3)");
                return DataShape.Scalar;
        }
        return ComputeResult.Invalid;
    }
    
    public ComputeResult Compute_AddTemp(Query query, ExpressionSyntax expressionSyntax, string comment, out string temp, bool useIdentifier)
    {
        if (useIdentifier && expressionSyntax is MemberAccessExpressionSyntax memberAccess) {
            var memberExpression = memberAccess.Expression;
            if (memberExpression is IdentifierNameSyntax identifierName) {
                temp = identifierName.Identifier.Text;
                return Vectorizer.GetShapeFromExpression(query, expressionSyntax);
            }
        }
        temp = query.AddTemp();
        var tempLanes = new StringBuilder[query.laneCount];
        query.computeTemp.AppendLine($"                    //   {comment}");
        for (int n = 0; n < tempLanes.Length; n++) {
            tempLanes[n] = new StringBuilder();
            tempLanes[n].Append($"                    Vector256<float> {temp}_{n} = ");
        }
        var shape = Compute(tempLanes, query, expressionSyntax); 
        if (!shape) {
            return ComputeResult.Invalid;
        }
        tempLanes.Append(";");
        for (int n = 0; n < tempLanes.Length; n++) {
            query.computeTemp.Append(tempLanes[n]);
            query.computeTemp.AppendLine();
        }
        query.computeTemp.AppendLine();
        return shape;
    } 

    public ComputeResult Method_Distance(StringBuilder[] lanes, Query query, ArgumentListSyntax argList, string method)
    {
        query.requireDeinterleave = true;
        var args = argList.Arguments;
        if (!Compute_AddTemp(query, args[0].Expression, $"{method} arg[0]", out var arg0, true)) {
            return ComputeResult.Invalid;
        }
        if (!Compute_AddTemp(query, args[1].Expression, $"{method} arg[1]", out var arg1, true)) {
            return ComputeResult.Invalid;
        }
        switch (query.vectorDimension)
        {
            case 2:
                lanes[0].Append($"AvxVector2.{method}({arg0}_0,{arg0}_1, {arg1}_0,{arg1}_1)");
                lanes[1].Append($"AvxVector2.{method}({arg0}_2,{arg0}_3, {arg1}_2,{arg1}_3)");
                return DataShape.Scalar;
            case 3:
                lanes[0].Append($"AvxVector3.{method}({arg0}_0,{arg0}_1,{arg0}_2, {arg1}_0,{arg1}_1,{arg1}_2)");
                return DataShape.Scalar;
            case 4:
                lanes[0].Append($"AvxVector4.{method}({arg0}_0,{arg0}_1,{arg0}_2,{arg0}_3, {arg1}_0,{arg1}_1,{arg1}_2,{arg1}_3)");
                return DataShape.Scalar;
        }
        return ComputeResult.Invalid;
    }
}