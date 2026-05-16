// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.Generators.WGSL;

public sealed partial class WgslVectorizer : IVectorizer
{
    public bool Emit(Query query)
    {
        query.ResetQueryState();
        query.laneCount     = 1;
        query.isWgslLane  = true;
        
        TraverseBody(query);
        return true;
    }
    
    public bool TraverseBody(Query query)
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
                    // var statementText = Regex.Replace(statement.ToString(), @"\s+", " ").Trim();
                    // compute.AppendLine($"        // {statementText}");
                    compute.Append(query.computeTemp);
                    query.computeTemp.Clear();
                    var lanes = query.lanes;
                    for (int n = 0; n < lanes.Length; n++) {
                        compute.Append($"        {lanes[n]}");
                    }
                    compute.AppendLine();
                }
                EmitVectorizedMethod(query, compute, body);
            }
        }
        return true;
    }
    
    public void EmitVectorizedMethod(Query query, StringBuilder compute, BlockSyntax? body)
    {
        var vectorizeBody = EmitBody(query, compute, body, 0);

        query.wgslBody = vectorizeBody.ToString();
    }
    
    
    public bool EmitCompute(Query query, StringBuilder[] lanes, StatementSyntax statement)
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
                        lanes[n].Append($"var {variableName} = ");
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
    
    public StringBuilder EmitBody(Query query, StringBuilder compute, BlockSyntax? body, int step)
    {
        var source = new StringBuilder();
        foreach (var vectorType in query.VectorTypes) {
            EmitLoadVector(source, query, vectorType, 0);
        }
        source.AppendLine();
        
        source.Append(compute);
        source.AppendLine();
        
        if (body == null) {
            return source;
        }
        foreach (var dirtyVector in query.dirtyVectors) {
            EmitStoreVector(source, query, dirtyVector, 0);
        }
        return source;
    }
    
    public ComputeResult Compute(StringBuilder[] lanes, Query query, ExpressionSyntax syntax)
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
            return Vectorizer.Compute_IdentifierName(lanes, query, identifier);
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
