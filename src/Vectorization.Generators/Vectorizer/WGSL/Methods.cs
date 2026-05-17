// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;


// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.Generators.WGSL;

public sealed partial class WgslVectorizer
{
    public ComputeResult Compute_Invocation(StringBuilder[] lanes, Query query, InvocationExpressionSyntax invocation)
    {
        var methodName = Symbols.GetMethodName(query, invocation);
        var methodReduced = methodName?.Replace("System.Numerics.Vector2", "Vector")
                                       .Replace("System.Numerics.Vector3", "Vector")
                                       .Replace("System.Numerics.Vector4", "Vector");
        var argList = invocation.ArgumentList;
        switch (methodReduced)
        {
            case "System.MathF.Sin(float)":         return Method_Scalar    (lanes, query, argList, "sin");
            case "System.MathF.Cos(float)":         return Method_Scalar    (lanes, query, argList, "cos");
            case "System.MathF.Tan(float)":         return Method_Scalar    (lanes, query, argList, "tan");
            case "System.MathF.Asin(float)":        return Method_Scalar    (lanes, query, argList, "asin");
            case "System.MathF.Acos(float)":        return Method_Scalar    (lanes, query, argList, "acos");
            case "System.MathF.Atan(float)":        return Method_Scalar    (lanes, query, argList, "atan");
            case "System.MathF.Atan2(float, float)":return Method_Scalar    (lanes, query, argList, "atan2");
            case "System.MathF.Asinh(float)":       return Method_Scalar    (lanes, query, argList, "asinh");
            case "System.MathF.Acosh(float)":       return Method_Scalar    (lanes, query, argList, "acosh");
            case "System.MathF.Atanh(float)":       return Method_Scalar    (lanes, query, argList, "atanh");
            case "System.MathF.Sinh(float)":        return Method_Scalar    (lanes, query, argList, "sinh");
            case "System.MathF.Cosh(float)":        return Method_Scalar    (lanes, query, argList, "cosh");
            case "System.MathF.Tanh(float)":        return Method_Scalar    (lanes, query, argList, "tanh");
            
            case "Vector.Abs(Vector)":              return Method_Abs       (lanes, query, argList, DataShape.Vector);
            case "System.MathF.Abs(float)":         return Method_Abs       (lanes, query, argList, DataShape.Scalar);
            case "System.MathF.Sign(float)":        return Method_Scalar    (lanes, query, argList, "sign");
            case "Vector.Truncate(Vector)":         return Method_Truncate  (lanes, query, argList, DataShape.Vector);
            case "System.MathF.Truncate(float)":    return Method_Truncate  (lanes, query, argList, DataShape.Scalar);
            case "Vector.Round(Vector)":            return Method_Round     (lanes, query, argList, DataShape.Vector);
            case "System.MathF.Round(float)":       return Method_Round     (lanes, query, argList, DataShape.Scalar);
            case "System.MathF.Floor(float)":       return Method_Floor     (lanes, query, argList);
            case "System.MathF.Ceiling(float)":     return Method_Ceiling   (lanes, query, argList);
            
            case "System.MathF.Exp(float)":         return Method_Scalar    (lanes, query, argList, "exp");
            case "System.MathF.Log(float)":         return Method_Scalar    (lanes, query, argList, "log");     // in WGSL: log = ln
            case "System.MathF.Log10(float)":       return Method_Scalar    (lanes, query, argList, "log10");
            case "System.MathF.Log2(float)":        return Method_Scalar    (lanes, query, argList, "log2");
            case "System.MathF.Pow(float, float)":  return Method_Scalar    (lanes, query, argList, "pow");
            case "System.MathF.Sqrt(float)":        return Method_Scalar    (lanes, query, argList, "sqrt");
            
            case "System.MathF.Min(float, float)":          return Method_MinMax    (lanes, query, argList, DataShape.Scalar, "min");
            case "Vector.Min(Vector, Vector)":              return Method_MinMax    (lanes, query, argList, DataShape.Vector, "min");
            
            case "System.MathF.Max(float, float)":          return Method_MinMax    (lanes, query, argList, DataShape.Scalar, "max");
            case "Vector.Max(Vector, Vector)":              return Method_MinMax    (lanes, query, argList, DataShape.Vector, "max");
            
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
        var args = argList.Arguments;
        // 1. matrix first (WGSL Standard: matrix * vector)
        if (args[1].Expression is IdentifierNameSyntax identifierName) {
            lanes.Append(identifierName.Identifier.Text);
        } else {
            if (!Compute(lanes, query, args[1].Expression)) return ComputeResult.Invalid;
        }
        lanes.Append(" * ");

        // 2. vector second - if vector is Vector3 we have to convert to vec4f first
        if (query.vectorDimension == 3) {
            lanes.Append("vec4f(");
            if (!Compute(lanes, query, args[0].Expression)) return ComputeResult.Invalid;
            lanes.Append(", 1.0)"); // 1.0 for position (Transform), 0.0 for direction
        } else {
            if (!Compute(lanes, query, args[0].Expression)) return ComputeResult.Invalid;
        }

        return DataShape.Vector;
    }

    public ComputeResult Method_MinMax(StringBuilder[] lanes, Query query, ArgumentListSyntax argList, DataShape shape, string op)
    {
        var args = argList.Arguments;
        lanes[0].Append($"{op}(");
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
        lanes.Append("clamp(");
        if (!Compute(lanes, query, args[0].Expression)) { // value
            return ComputeResult.Invalid;
        }
        lanes.Append(", ");
        if (!Compute(lanes, query, args[1].Expression)) { // low
            return ComputeResult.Invalid;
        }
        lanes.Append(", ");
        if (!Compute(lanes, query, args[2].Expression)) { // high
            return ComputeResult.Invalid;
        }
        lanes.Append(")");
        return shape;
    }
    
    public ComputeResult Method_Lerp(StringBuilder[] lanes, Query query, ArgumentListSyntax argumentSyntax)
    {
        var args = argumentSyntax.Arguments;
        lanes.Append("mix(");   // WGSL: mix(a, b, t)
        if (!Compute(lanes, query, args[0].Expression)) {   // start value: a
            return ComputeResult.Invalid;
        }
        lanes.Append(", ");
        if (!Compute(lanes, query, args[1].Expression)) {   // end value: b
            return ComputeResult.Invalid;
        }
        lanes.Append(", ");
        if (!Compute(lanes, query, args[2].Expression)) {   // interpolation factor: t
            return ComputeResult.Invalid;
        }
        lanes.Append(")");
        return DataShape.Vector;
    }

    public ComputeResult Method_Abs(StringBuilder[] lanes, Query query, ArgumentListSyntax argList, DataShape shape)
    {
        lanes.Append("abs(");
        var args = argList.Arguments;
        if (!Compute(lanes, query, args[0].Expression)) {
            return ComputeResult.Invalid;
        }
        lanes.Append(")");
        return shape;
    }
    
    public ComputeResult Method_Truncate(StringBuilder[] lanes, Query query, ArgumentListSyntax argList, DataShape shape)
    {
        lanes.Append("trunc(");
        var args = argList.Arguments;
        if (!Compute(lanes, query, args[0].Expression)) {
            return ComputeResult.Invalid;
        }
        lanes.Append(")");
        return shape;
    }
    
    public ComputeResult Method_Floor(StringBuilder[] lanes, Query query, ArgumentListSyntax argList)
    {
        lanes.Append("floor(");
        var args = argList.Arguments;
        if (!Compute(lanes, query, args[0].Expression)) {
            return ComputeResult.Invalid;
        }
        lanes.Append(")");
        return DataShape.Scalar;
    }
    
    public ComputeResult Method_Ceiling(StringBuilder[] lanes, Query query, ArgumentListSyntax argList)
    {
        lanes.Append("ceil(");
        var args = argList.Arguments;
        if (!Compute(lanes, query, args[0].Expression)) {
            return ComputeResult.Invalid;
        }
        lanes.Append(")");
        return DataShape.Scalar;
    }
    
    public ComputeResult Method_Round(StringBuilder[] lanes, Query query, ArgumentListSyntax argList, DataShape shape)
    {
        lanes.Append("round(");
        var args = argList.Arguments;
        if (!Compute(lanes, query, args[0].Expression)) {
            return ComputeResult.Invalid;
        }
        lanes.Append(")");
        return shape;
    }

    public ComputeResult Method_Scalar(StringBuilder[] lanes, Query query, ArgumentListSyntax argList, string method)
    {
        if (method == "log10") query.wgslHelperMethods.Add("log10");

        lanes[0].Append($"{method}(");
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
        var args = argList.Arguments;
        var dim = query.vectorDimension;

        switch (dim) {
            case 2:
                query.wgslHelperMethods.Add("cross2d");  // 2D:  result = a.x * b.y - a.y * b.x (Scalar)
                lanes.Append("cross2d(");
                if (!Compute(lanes, query, args[0].Expression)) return ComputeResult.Invalid;
                lanes.Append(", ");
                if (!Compute(lanes, query, args[1].Expression)) return ComputeResult.Invalid;
                lanes.Append(")");
                return DataShape.Scalar;
            case 3:
                lanes.Append("cross("); // standard WGSL cross(vec3f, vec3f)
                if (!Compute(lanes, query, args[0].Expression)) return ComputeResult.Invalid;
                lanes.Append(", ");
                if (!Compute(lanes, query, args[1].Expression)) return ComputeResult.Invalid;
                lanes.Append(")");
                return DataShape.Vector;
            case 4:
                lanes.Append("vec4f(cross("); // treat Vector4 Cross as Vector3 cross (ignore W)
                lanes.Append("vec3f(");
                if (!Compute(lanes, query, args[0].Expression)) return ComputeResult.Invalid;
                lanes.Append("), vec3f(");
                if (!Compute(lanes, query, args[1].Expression)) return ComputeResult.Invalid;
                lanes.Append(")), 0.0)");
                return DataShape.Vector;
            default:
                query.Diagnostics.ReportDiagnosticSyntax(Errors.OperationUnsupported, argList, $"Cross for dimension {dim}");
                return ComputeResult.Invalid;
        }
    }
    
    public ComputeResult Method_Normalize(StringBuilder[] lanes, Query query, ArgumentListSyntax argList)
    {
        lanes.Append("normalize(");
        
        var args = argList.Arguments;
        if (!Compute(lanes, query, args[0].Expression)) {
            return ComputeResult.Invalid;
        }
        lanes.Append(")");
        return DataShape.Vector;
    }
    
    public ComputeResult Method_Length(StringBuilder[] lanes, Query query, InvocationExpressionSyntax invocation)
    {
        lanes.Append("length(");
        
        var expression = invocation.Expression;
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess) {
            expression = memberAccess.Expression;
        }
        if (!Compute(lanes, query, expression)) { 
            return ComputeResult.Invalid;
        }
        lanes.Append(")");
        return DataShape.Scalar;
    }
    
    public ComputeResult Compute_AddTemp(Query query, ExpressionSyntax expressionSyntax, string comment, out string temp, bool useIdentifier)
    {
        throw new Exception("Unexpected call. WGSL requires no temp variables for flattening");
    } 

    public ComputeResult Method_Distance(StringBuilder[] lanes, Query query, ArgumentListSyntax argList, string method)
    {
        var args = argList.Arguments;
        var dim = query.vectorDimension;
        
        if (method == "Distance") {
            lanes.Append("distance(");
        } else if (method == "DistanceSquared") {
            var helperName = $"distanceSquared{dim}"; // e.g. register distanceSquared3()
            query.wgslHelperMethods.Add(helperName);
            lanes.Append($"{helperName}(");
        }
        if (!Compute(lanes, query, args[0].Expression)) return ComputeResult.Invalid;
        lanes.Append(", ");
        if (!Compute(lanes, query, args[1].Expression)) return ComputeResult.Invalid;
        lanes.Append(")");
        
        return DataShape.Scalar;
    }
}