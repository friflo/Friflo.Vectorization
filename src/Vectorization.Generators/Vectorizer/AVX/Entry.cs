// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.Generators.AVX;

public sealed partial class AvxVectorizer : IVectorizer
{
    public bool Emit(Query query)
    {
        var vectorTypes = query.VectorTypes;
        if (vectorTypes.Length == 0) {
            return false;
        }
        var vectorTypeDimension = VectorType.GetVectorTypeDimension(query, vectorTypes);
        if (vectorTypeDimension == 0) {
            return false;
        }
        query.vectorDimension = vectorTypeDimension;
        
        // --- Phase 1: Layout Analysis ---
        Strategy initialStrategy;
        bool allSoA = vectorTypes.All(p => p.Layout == VectorLayout.AoSoA);
        bool allAoS = vectorTypes.All(p => p.Layout == VectorLayout.AoS);
        if (allSoA) {
            initialStrategy = Strategy.NativeSoA;
        } else if (allAoS) {
            initialStrategy = Strategy.VerticalAoS;
        } else {
            initialStrategy = Strategy.MixedAdapter;
        }
        query.strategy = initialStrategy;

        if (query.VectorMode == VectorMode.Vector && query.Spans.Length == 0) {
            query.Diagnostics.ReportDiagnosticSymbol(Errors.MissingSpanParameter, null, []);
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
            query.ResetQueryState();
            // Reset query state created by previous traversal. Generated code require Deinterleave() / Interleave()
            query.useDeinterleave = true;
            query.avxMethod = "";
            
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
    public bool Emit_NativeSoA   (Query query) => TraverseBody(query);
    public bool Emit_VerticalAoS (Query query) => TraverseBody(query);
    public bool Emit_MixedAdapter(Query query) => TraverseBody(query);
    public bool Emit_Horizontal  (Query query) {
        query.strategy = Strategy.Horizontal;
        return TraverseBody(query);
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
    
    public string EmitVectorizeBlock(Query query)
    {
        if (!query.vectorized) {
            return "";
        }
        var sb = new StringBuilder();
        for (int n = 0; n < query.VectorTypes.Length; n++) {
            var vectorType = query.VectorTypes[n];
            sb.Append(", ");
            var parameter = vectorType.Parameter;
            if (vectorType.IsSpan) {
                sb.Append($"{parameter.Name}Span");
                /* if (vectorType.layout == VectorLayout.SoA) {
                    sb.Append($", chunk.Chunk{n+1}.GetStrideSoA()");
                } */
                continue;
            }
            GeneratorUtils.AppendRefKind(sb, parameter.RefKind);
            sb.Append(parameter.Name);
        }
        var avxMethod = query.CustomMethod ?? $"_{query.BlueprintMethod.Name}_Avx{query.Hash}";
        var source = $@"
                if (!vectorized) goto EntityLoop;
                if (Avx.IsSupported) {{
                    n = {avxMethod}(_entities.Length{sb});
                }}
            EntityLoop:";
        return source;
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
    
    public void EmitVectorizedMethod(Query query, StringBuilder compute, BlockSyntax? body)
    {
        var locals = new StringBuilder();
        // --- method signature
        var signature = new StringBuilder();
        foreach (var vectorType in query.VectorTypes) {
            var parameter = vectorType.Parameter;
            signature.Append(",");
            if (vectorType.IsSpan) {
                if (vectorType.ParamType == ParamType.Scalar) {
                    AvxUtils.ScalarMask(locals, parameter.Name, query.vectorDimension);
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
                    AvxUtils.InterleaveVector3(locals, parameter.Name, query);
                    locals.AppendLine();
                    break;
                case ParamType.Matrix4x4:
                    AvxUtils.LoadMatrix(locals, parameter.Name, query.vectorDimension);
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
        var vectorizeBody = EmitBody(query, compute, body, step);

        AvxUtils.TrimEnd(vectorizeBody);
        
        var strategyComment = query.strategy switch {
            Strategy.NativeSoA      => "// [Layout: [SoA] All]     - lane-native speed",
            Strategy.VerticalAoS    => "// [Layout: AoS-Vertical]  - lane-native speed",
            Strategy.MixedAdapter   => "// [Layout: AoS-SoA-Mixed] - lane-native speed + Deinterleave penalty",
            Strategy.Horizontal     => "// [Layout: Horizontal]    - lane-native speed + Deinterleave penalty",
        };
        var guards = EmitLengthGuards(query);
        bool isQuery = query.VectorMode == VectorMode.Query;
        var source = $@"
        {strategyComment}
        [SkipLocalsInit]
        private static unsafe int _{query.BlueprintMethod.Name}_Avx{query.Hash}(int count{signature})
        {{
            {(isQuery ? $@"int paddedCount = (count + {elementStep - 1}) & ~{elementStep - 1};
            int i = 0;
" : $@"int i = 0;
            count -= {elementStep};
            if (i > count) {{
                return 0;
            }}
")}{guards}
{localBlock}{@fixed}            {{{pointerAssignment}

                for (; {(isQuery ? "i < paddedCount" : "i <= count")}; i += {elementStep})
                {{
{vectorizeBody}

{pointerIncrement}                }}
            }}
            return i;
        }}
";
        query.avxMethod = source;
    }
    
    public StringBuilder EmitLengthGuards(Query query)
    {
        var sb = new StringBuilder();
        var count = query.VectorMode == VectorMode.Query ? "paddedCount" : "count";
        foreach (var vectorType in query.VectorTypes) {
            if (!vectorType.IsSpan) continue;
            var name = vectorType.Name;
            if (vectorType.Layout == VectorLayout.AoSoA) {
                // sb.AppendLine($"            if ({name}.Length < {count} + {name}_stride * {vectorType.dimension - 1}) VectorUtils.ThrowBufferTooSmall(nameof({name}));");
                sb.AppendLine($"            if ({name}.Length < {count}) VectorUtils.ThrowBufferTooSmall(nameof({name}));");
            } else {
                sb.AppendLine($"            if ({name}.Length < {count}) VectorUtils.ThrowBufferTooSmall(nameof({name}));");
            }
        }
        return sb;
    }
    
    public StringBuilder EmitBody(Query query, StringBuilder compute, BlockSyntax? body, int step)
    {
        var source = new StringBuilder();
        source.AppendLine("                    // --- 1. Load");
        foreach (var vectorType in query.VectorTypes) {
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
            return Symbols.Compute_IdentifierName(lanes, query, identifier);
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
