// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

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
    
    
    // --- Methods.cs
    
    
    // --- Operators.cs
    
    
    // --- Symbols.cs
    
}