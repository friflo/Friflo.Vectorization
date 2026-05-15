// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;
using Microsoft.CodeAnalysis;

// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.Generators;

/// <summary>
/// The ONLY intention of IVectorizer is that all Vectorizer implementations follow the same pattern.<br/>
/// The interface methods are never called (and must not) via an IVectorizer reference.
/// </summary>
public interface IVectorizer
{
    // --- Entry.cs
    public  bool            Emit(Query query);
    public  bool            TraverseBody(Query query);
    public  bool            EmitCompute(Query query, StringBuilder[] lanes, StatementSyntax statement);
    public  void            EmitVectorizedMethod(Query query, StringBuilder compute, BlockSyntax? body);
    public  StringBuilder   EmitLoopBody(Query query, StringBuilder compute, BlockSyntax? body, int step);
    public  ComputeResult   Compute(StringBuilder[] lanes, Query query, ExpressionSyntax syntax);
    
    // --- IO.cs
    public  void            EmitLoadVector (StringBuilder source, Query query, VectorType vectorType, int step);
    public  void            EmitStoreVector(StringBuilder source, Query query, string dirtyVector, int step);
    
    // --- Methods.cs
    public  ComputeResult   Compute_Invocation  (StringBuilder[] lanes, Query query, InvocationExpressionSyntax invocation);
    public  ComputeResult   Method_Vector4_Transform(StringBuilder[] lanes, Query query, ArgumentListSyntax argumentSyntax);
    public  ComputeResult   Method_MinMax       (StringBuilder[] lanes, Query query, DataShape shape, string op, ArgumentListSyntax argumentSyntax);
    public  ComputeResult   Method_Clamp        (StringBuilder[] lanes, Query query, DataShape shape, ArgumentListSyntax argumentSyntax);
    public  ComputeResult   Method_Lerp         (StringBuilder[] lanes, Query query, ArgumentListSyntax argumentSyntax);
    public  ComputeResult   Method_Abs          (StringBuilder[] lanes, Query query, DataShape shape, ArgumentListSyntax argumentSyntax);
    public  ComputeResult   Method_Truncate     (StringBuilder[] lanes, Query query, DataShape shape, ArgumentListSyntax argumentSyntax);
    public  ComputeResult   Method_Floor        (StringBuilder[] lanes, Query query, ArgumentListSyntax argumentSyntax);
    public  ComputeResult   Method_Ceiling      (StringBuilder[] lanes, Query query, ArgumentListSyntax argumentSyntax);
    public  ComputeResult   Method_Round        (StringBuilder[] lanes, Query query, DataShape shape, ArgumentListSyntax argumentSyntax);
    public  ComputeResult   Method_Scalar       (StringBuilder[] lanes, Query query, string method, ArgumentListSyntax argumentSyntax);
    public  ComputeResult   Method_Cross        (StringBuilder[] lanes, Query query, ArgumentListSyntax argumentSyntax);
    public  ComputeResult   Method_Normalize    (StringBuilder[] lanes, Query query, ArgumentListSyntax argumentSyntax);
    public  ComputeResult   Method_Length       (StringBuilder[] lanes, Query query, InvocationExpressionSyntax invocation);
    public  ComputeResult   Compute_AddTemp     (Query query, ExpressionSyntax expressionSyntax, string comment, out string temp, bool useIdentifier);
    public  ComputeResult   Method_Distance     (StringBuilder[] lanes, Query query, ArgumentListSyntax argumentSyntax, string method);
    
    // --- Operators.cs
    public StringBuilder[]  CreateLanes(Query query, ISymbol? symbol, string parameterName);
    public  ComputeResult   Compute_Assignment(StringBuilder[] lanes, Query query, AssignmentExpressionSyntax assignment);
    public  ComputeResult   Compute_Binary(StringBuilder[] lanes, Query query, BinaryExpressionSyntax binary);
    
    // --- Symbols.cs
    public  ComputeResult   Compute_MemberAccess(StringBuilder[] lanes, Query query, MemberAccessExpressionSyntax memberAccess);
    public  ComputeResult   Compute_IdentifierName(StringBuilder[] lanes, Query query, IdentifierNameSyntax identifierName);
    public  string?         GetMethodName(Query query, InvocationExpressionSyntax invocation);
    public  ComputeResult   Compute_Literal(StringBuilder[] lanes, Query query, LiteralExpressionSyntax literal);
}
