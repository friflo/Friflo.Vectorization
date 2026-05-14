// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.Generators.WGSL;

public static partial class WgslVectorizer
{
    public static bool Emit(Query query)
    {
        TraverseBody(query);
        return true;
    }
    
    private static bool TraverseBody(Query query)
    {
        foreach (var type in query.VectorTypes) {
            query.AddParam(type.Parameter.Name, type.IsSpan, type.IsScalar, true, type.Dimension);
        }
        foreach (var syntaxReference in query.BlueprintMethod.DeclaringSyntaxReferences)
        {
            SyntaxNode node = syntaxReference.GetSyntax();
            if (node is MethodDeclarationSyntax methodDeclarationSyntax) {
                var body = methodDeclarationSyntax.Body;
                if (body == null) continue;
                var compute = new StringBuilder();
                foreach (var statement in body.Statements) {
                    if (!EmitCompute(query, null!, statement)) {
                        return false;
                    }
                    var statementText = Regex.Replace(statement.ToString(), @"\s+", " ").Trim();
                    compute.AppendLine($"                    // {statementText}");
                    compute.Append(query.computeTemp);
                    query.computeTemp.Clear();
                    var lanes = query.lanes;
                    for (int n = 0; n < lanes.Length; n++) {
                        compute.AppendLine($"                    {lanes[n]}");
                    }
                    compute.AppendLine();
                }
                EmitVectorizedMethod(query, compute, body);
            }
        }
        return true;
    }
    
    private static void EmitVectorizedMethod(Query query, StringBuilder compute, BlockSyntax? body)
    {
        var locals = new StringBuilder();
        // --- method signature
        var signature = new StringBuilder();
        foreach (var vectorType in query.VectorTypes) {
            var parameter = vectorType.Parameter;
            signature.Append(",");
            if (vectorType.IsSpan) {
                if (vectorType.ParamType == ParamType.Scalar) {
                    VectorUtils.ScalarMask(locals, parameter.Name, query.vectorDimension);
                }
                if (vectorType.Layout == VectorLayout.AoSoA) {
                    signature.Append($"\n            Span<float> {parameter.Name}"); // , int {parameter.Name}_stride");
                    continue;
                }
                var span = parameter.RefKind == RefKind.Ref ? "Span" : "ReadOnlySpan";
                signature.Append($"\n            {span}<{vectorType.FullQualifiedName}> {parameter.Name}");
                continue;
            }
            signature.Append("\n            ");
            GeneratorUtils.AppendRefKind(signature, parameter.RefKind);
            signature.Append($"{vectorType.FullQualifiedName} {parameter.Name}");
            //
            switch (vectorType.ParamType) {
                case ParamType.Scalar:
                    locals.AppendLine($"            var {parameter.Name}_scalar = Vector256.Create({parameter.Name});");
                    locals.AppendLine();
                    break;
                default:                // TODO  type should be clear here 
                case ParamType.Vector:
                    VectorUtils.InterleaveVector3(locals, parameter.Name, query);
                    locals.AppendLine();
                    break;
                case ParamType.Matrix4x4:
                    VectorUtils.LoadMatrix(locals, parameter.Name, query.vectorDimension);
                    locals.AppendLine();
                    break;
            }
        }
        // const locals
        locals.Append(query.locals);

        var localBlock = "";
        if (locals.Length > 0) {
            localBlock = $"            // --- Locals\n{locals}";
        }
        
        // --- fixed block
        var @fixed = new StringBuilder();
        foreach (var span in query.Spans) {
            var vectorType = span.VectorType!;
            var type = vectorType.Layout == VectorLayout.AoSoA
                ? "float"
                : vectorType.Parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            @fixed.Append($"            fixed ({type}* {vectorType.Name}_first = {vectorType.Name})");
            @fixed.AppendLine();
        }
        // --- pointer assignment
        var pointerAssignment = new StringBuilder();
        var pointerIncrement  = new StringBuilder();
        foreach (var vectorType in query.VectorTypes) {
            if (!vectorType.IsSpan) continue;
            pointerAssignment.AppendLine();
            pointerAssignment.Append($"                float* {vectorType.Name}_ptr = (float*){vectorType.Name}_first;");
            
            var increment = vectorType.Dimension * query.scalarLaneCount * 8;
            pointerIncrement.AppendLine($"                    {vectorType.Name}_ptr += {increment};");
        }
        var elementStep = query.vectorDimension switch {
            1 => 32,
            2 => 16,
            3 => 8,
            4 => 8,
            _ => -1,
        };
        int step = 8;



        



        var source = $@"

";
        query.avxMethod = source;
    }
    
    
    private static bool EmitCompute(Query query, StringBuilder[] lanes, StatementSyntax statement)
    {
        // Is local declaration - e.g.     var local = value;
        if (statement is LocalDeclarationStatementSyntax localDecl) {
            foreach (var variable in localDecl.Declaration.Variables) {
                var initializerExpression = variable.Initializer?.Value;
                if (initializerExpression != null) {
                    var variableName = variable.Identifier.Text;
                    var symbol = query.SemanticModel.GetDeclaredSymbol(variable);
                    lanes = CreateLanes(query, symbol, variableName);
                    for (int n = 0; n < lanes.Length; n++) {
                        lanes[n].Append($"var {variableName}_{n} = ");
                    }
                    if (!Compute(lanes, query, initializerExpression)) {
                        return false;
                    }
                    lanes.Append(";");
                }
            }
            return true;
        }
        // Assignment - e.g.     position.value = value;
        if (statement is ExpressionStatementSyntax expressionStatement) {
            var expressionSyntax = expressionStatement.Expression;
            if (!Compute(lanes, query, expressionSyntax)) {
                return false;
            }
            return true;
        }
        query.Diagnostics.ReportDiagnosticSyntax(Errors.StatementUnsupported, statement, statement.ToFullString());
        return false;
    }
    
    private static ComputeResult Compute(StringBuilder[] lanes, Query query, ExpressionSyntax syntax)
    {
        if (syntax is AssignmentExpressionSyntax assignment) {
            return Compute_Assignment(lanes, query, assignment);
        }
        if (syntax is BinaryExpressionSyntax binary) {
            return Compute_Binary(lanes, query, binary);
        }
        if (syntax is MemberAccessExpressionSyntax memberAccess) {
            return Compute_MemberAccess(lanes, query, memberAccess);
        }
        if (syntax is IdentifierNameSyntax identifier) {
            return Compute_IdentifierName(lanes, query, identifier);
        }
        if (syntax is InvocationExpressionSyntax invocation) {
            return Compute_Invocation(lanes, query, invocation);
        }
        if (syntax is LiteralExpressionSyntax literal) {
            return Compute_Literal(lanes, query, literal);
        }
        query.Diagnostics.ReportDiagnosticSyntax(Errors.OperationUnsupported, syntax, syntax.ToFullString());
        return ComputeResult.Invalid;
    }
}
