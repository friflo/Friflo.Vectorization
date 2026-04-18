// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Friflo.Vectorization.Generators;

public static partial class Vectorizer
{
    public static bool Emit(Query query)
    {
        var vectorTypes = VectorType.GetVectorTypes(query);
        if (vectorTypes == null) {
            return false;
        }
        var vectorTypeDimension = VectorType.GetVectorTypeDimension(query, vectorTypes);
        if (vectorTypeDimension == 0) {
            return false;
        }
        // --- Phase 1: Layout Analysis ---
        Strategy initialStrategy;
        bool allSoA = vectorTypes.All(p => p.layout == VectorLayout.SoA);
        bool allAoS = vectorTypes.All(p => p.layout == VectorLayout.AoS);
        if (allSoA) {
            initialStrategy = Strategy.NativeSoA;
        } else if (allAoS) {
            initialStrategy = Strategy.VerticalAoS;
        } else {
            initialStrategy = Strategy.MixedAdapter;
        }
        query.strategy = initialStrategy;
        
        query.vectorTypes = vectorTypes;
        query.vectorDimension = vectorTypeDimension;
        if (query.VectorMode == VectorMode.Vector && query.Spans.Count == 0) {
            query.ReportDiagnosticSymbol(Errors.MissingSpanParameter, null, []);
            return false;
        }
        query.laneCount = query.vectorDimension switch {
            // Aiming for loop unroll factor 4 which is typically the Sweet Spot
            1 => 4,
            2 => 4,
            3 => 3,
            4 => 4,
            _ => -1
        };
        query.scalarLaneCount = query.vectorDimension switch {
            // Aiming for loop unroll factor 4 which is typically the Sweet Spot
            1 => 4,
            2 => 2,
            3 => 1,
            4 => 1,
            _ =>-1
        };

        // 1. Pass
        var success = initialStrategy switch {
            Strategy.NativeSoA      => Emit_NativeSoA(query),
            Strategy.VerticalAoS    => Emit_VerticalAoS(query),
            Strategy.MixedAdapter   => Emit_MixedAdapter(query),
        };
        if (!success) {
            return false;
        }
        if (query.requireDeinterleave) {
            // 2. Pass
            ResetQueryState(query);
            success = initialStrategy switch {
                Strategy.VerticalAoS    => Emit_Horizontal(query),
                Strategy.MixedAdapter   => Emit_MixedAdapter(query),
            };
            if (!success) {
                return false;
            }
        }
        query.vectorized = true;
        return true;
    }
    private static bool Emit_NativeSoA   (Query query) => TraverseBody(query);
    private static bool Emit_VerticalAoS (Query query) => TraverseBody(query);
    private static bool Emit_MixedAdapter(Query query) => TraverseBody(query);
    private static bool Emit_Horizontal  (Query query) {
        query.strategy = Strategy.Horizontal;
        return TraverseBody(query);
    }

    private static void ResetQueryState(Query query)
    {
        // Reset query state created by previous traversal. Generated code require Deinterleave() / Interleave()
        query.useDeinterleave = true;
        query.avxMethod = "";
        query.lanes = null;
        query.paramTypes.Clear();
        query.locals.Clear();
        query.computeTemp.Clear();
        query.computeTempCount = 0;
        query.constLocalsCount = 0;
    }
    
    private static bool TraverseBody(Query query)
    {
        foreach (var type in query.vectorTypes) {
            query.AddParam(type.parameter.Name, type.isSpan, type.isScalar, true, type.dimension);
        }
        foreach (var syntaxReference in query.BlueprintMethod.DeclaringSyntaxReferences)
        {
            SyntaxNode node = syntaxReference.GetSyntax();
            if (node is MethodDeclarationSyntax methodDeclarationSyntax) {
                var body = methodDeclarationSyntax.Body;
                if (body == null) continue;
                var compute = new StringBuilder();
                foreach (var statement in body.Statements) {
                    if (!EmitCompute(query, null!, compute, statement)) {
                        return false;
                    }
                    var statementText = Regex.Replace(statement.ToString(), @"\s+", " ").Trim();;
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
    
    public static string EmitVectorizeBlock(Query query)
    {
        if (!query.vectorized) {
            return "";
        }
        var sb = new StringBuilder();
        for (int n = 0; n < query.vectorTypes.Length; n++) {
            var vectorType = query.vectorTypes[n];
            sb.Append(", ");
            var parameter = vectorType.parameter;
            if (vectorType.isSpan) {
                sb.Append($"{parameter.Name}Span");
                if (vectorType.layout == VectorLayout.SoA) {
                    sb.Append($", chunk.Chunk{n+1}.GetStrideSoA()");
                }
                continue;
            }
            Utils.AppendRefKind(sb, parameter.RefKind);
            sb.Append(parameter.Name);
        }
        var source = $@"
                if (!vectorized) goto EntityLoop;
                if (Avx.IsSupported) {{
                    n = _{query.BlueprintMethod.Name}_Avx{query.Hash}(_entities.Length{sb});
                }}
            EntityLoop:";
        return source;
    }
    
    private static bool EmitCompute(Query query, StringBuilder[] lanes, StringBuilder compute, StatementSyntax statement)
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
            return Compute(lanes, query, expressionSyntax);
        }
        query.ReportDiagnosticSyntax(Errors.StatementUnsupported, statement, statement.ToFullString());
        return false;
    }
    
    private static void EmitVectorizedMethod(Query query, StringBuilder compute, BlockSyntax? body)
    {
        var locals = new StringBuilder();
        // --- method signature
        var signature = new StringBuilder();
        foreach (var vectorType in query.vectorTypes) {
            var parameter = vectorType.parameter;
            signature.Append(",");
            if (vectorType.isSpan) {
                if (vectorType.paramType == ParamType.Scalar) {
                    Utils.ScalarMask(locals, parameter.Name, query.vectorDimension);
                }
                if (vectorType.layout == VectorLayout.SoA) {
                    signature.Append($"\n            Span<float> {parameter.Name}, int {parameter.Name}_stride");
                    continue;
                }
                var span = parameter.RefKind == RefKind.Ref ? "Span" : "ReadOnlySpan";
                signature.Append($"\n            {span}<{vectorType.fullQualifiedName}> {parameter.Name}");
                continue;
            }
            signature.Append("\n            ");
            Utils.AppendRefKind(signature, parameter.RefKind);
            signature.Append($"{vectorType.fullQualifiedName} {parameter.Name}");
            //
            switch (vectorType.paramType) {
                case ParamType.Scalar:
                    locals.AppendLine($"            var {parameter.Name}_scalar = Vector256.Create({parameter.Name});");
                    locals.AppendLine();
                    break;
                default:                // TODO  type should be clear here 
                case ParamType.Vector:
                    Utils.InterleaveVector3(locals, parameter.Name, query);
                    locals.AppendLine();
                    break;
                case ParamType.Matrix4x4:
                    Utils.LoadMatrix(locals, parameter.Name);
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
            var type = Utils.HasAttribute(span.Type.GetAttributes(), "Friflo.Engine.ECS.SoAAttribute")
                ? "float"
                : span.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            @fixed.Append($"            fixed ({type}* {span.Name}_first = {span.Name})");
            @fixed.AppendLine();
        }
        // --- pointer block
        var pointer = new StringBuilder();
        foreach (var span in query.Spans) {
            pointer.AppendLine();
            pointer.Append($"                    float* {span.Name}_ptr = (float*)({span.Name}_first + i);");
        }
        var elementStep = query.vectorDimension switch {
            1 => 32,
            2 => 16,
            3 => 8,
            4 => 8,
            _ => -1,
        };
        int step = 8;
        var vectorizeBlock = EmitLoopBody(query, compute, body, step);

        Utils.TrimEnd(vectorizeBlock);
        
        var strategyComment = query.strategy switch {
            Strategy.NativeSoA      => "// [Layout: [SoA] All]     - lane-native speed",
            Strategy.VerticalAoS    => "// [Layout: AoS-Vertical]  - lane-native speed",
            Strategy.MixedAdapter   => "// [Layout: AoS-SoA-Mixed] - lane-native speed + Deinterleave penalty",
            Strategy.Horizontal     => "// [Layout: Horizontal]    - lane-native speed + Deinterleave penalty",
        };
        bool isQuery = false; // query.VectorMode == VectorMode.Query;
        var source = $@"
        {strategyComment}
        [SkipLocalsInit]
        private static unsafe int _{query.BlueprintMethod.Name}_Avx{query.Hash}(int count{signature})
        {{
            {(isQuery ? $@"int paddedCount = (count + {step - 1}) & ~{step - 1};
            int i = 0;
" : $@"int i = 0;
            count -= {elementStep};
            if (i > count) {{
                return 0;
            }}")}
{localBlock}{@fixed}            {{
                for (; {(isQuery ? "i < paddedCount" : "i <= count")}; i += {elementStep})
                {{{pointer}
{vectorizeBlock}
                }}
            }}
            return i;
        }}
";
        query.avxMethod = source;
    }
    
    private static StringBuilder EmitLoopBody(Query query, StringBuilder compute, BlockSyntax? body, int step)
    {
        var source = new StringBuilder();
        source.AppendLine();
        source.AppendLine("                    // --- 1. Load");
        foreach (var vectorType in query.vectorTypes) {
            EmitLoadVector(source, query, vectorType, step);
        }
        source.AppendLine("                    // --- 2. Compute");
        source.Append(compute);
        if (compute.Length == 0) source.AppendLine();
        
        source.AppendLine("                    // --- 3. Store");
        if (body == null) {
            return source;
        }
        foreach (var dirtyVector in query.dirtyVectors) {
            EmitStoreVector(source, query, dirtyVector, step); // DIRTY
        }
        return source;
    }
    
    private static bool Compute(StringBuilder[] lanes, Query query, ExpressionSyntax syntax)
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
        query.ReportDiagnosticSyntax(Errors.OperationUnsupported, syntax, syntax.ToFullString());
        return false;
    }
}
