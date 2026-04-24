// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Friflo.Vectorization.Generators;

public partial class AttributeQueryGenerator
{
    private static void EmitQuerySource(
        Query query,
        out string shadowMethodSource,
        out string privateSource)
    {
        var attributeCode       = EmitQueryFilters(query.Attributes);
        var componentArgs       = EmitQueryArgs(query.Spans);
        var chunkVariables      = EmitQueryChunkVariables(query.Spans);
        var lambdaParameters    = EmitQueryLambdaParameters(query);
        var methodSignature     = EmitQueryMethodSignature(query.Parameters, query.vectorized);
        var vectorizeBlock      = Vectorizer.EmitVectorizeBlock(query);
        EmitSoAGetterAndSetter(query.Spans, out var getterAoS, out var setterAoS);
        
        var hash            = query.Hash;
        var blueprintMethod = query.BlueprintMethod;
        var methodName      = query.BlueprintMethod.Name;
        
        if (query.Spans.Length == 0)
        {
            shadowMethodSource = $@"
        /// <summary>Query method generated for: <see cref=""{methodName}""/>.</summary>
        /// <returns>The executed <see cref=""ArchetypeQuery""/> for debugging purposes</returns>
        public {(blueprintMethod.IsStatic ? "static " : "")}ArchetypeQuery {methodName}Query({methodSignature})
        {{
            var _query = _{methodName}_GetQuery{hash}(_store);
            foreach (var entity in _query.Entities)
            {{
                {methodName}({lambdaParameters});
            }}
            return _query;
        }}";
        } else {
            shadowMethodSource = $@"
        /// <summary>Query method generated for: <see cref=""{methodName}""/>.</summary>
        /// <returns>The executed <see cref=""ArchetypeQuery""/> for debugging purposes</returns>
        public {(blueprintMethod.IsStatic ? "static " : "")}ArchetypeQuery {methodName}Query({methodSignature})
        {{
            var _query = _{methodName}_GetQuery{hash}(_store);
            foreach (var chunk in _query.Chunks)
            {{
                var _entities = chunk.Entities;
{chunkVariables}
                int n = 0;{vectorizeBlock}
                for (; n < _entities.Length; n++) {{
{getterAoS}                    {methodName}({lambdaParameters});
{setterAoS}                }}
            }}
            return _query;
        }}";
        }
        privateSource = $@"
        [EditorBrowsable(EditorBrowsableState.Never)]
        private static readonly int _{methodName}_Slot{hash} = EntityStore.UserDataNewSlot();

        [EditorBrowsable(EditorBrowsableState.Never)]
        private static ArchetypeQuery{componentArgs}
            _{methodName}_GetQuery{hash}(EntityStore _store)
        {{
            var _query = (ArchetypeQuery{componentArgs})
                EntityStore.UserDataGet(_store, _{methodName}_Slot{hash});
            if (_query != null) {{
                return _query;
            }}
            _query = _store.Query{componentArgs}();
{attributeCode}
            EntityStore.UserDataSet(_store, _{methodName}_Slot{hash}, _query);
            return _query;
        }}";
    }
    
    private static string EmitQueryMethodSignature(BlueprintParameter[] parameters, bool vectorized)
    {
        var sb = new StringBuilder();
        sb.Append("EntityStore _store");
        foreach (var parameter in parameters) {
            if (parameter.IsSpan) {
                continue;
            }
            var symbol = parameter.Symbol;
            if (parameter.IsEntity) {
                continue;
            }
            sb.Append(", ");
            Utils.AppendRefKind(sb, symbol.RefKind);
            string type = symbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            sb.Append(type);
            sb.Append(" ");
            sb.Append(symbol.Name);
        }
        if (vectorized) {
            sb.Append(", bool vectorized = true");
        }
        return sb.ToString();
    }
    
    private static string EmitQueryArgs(BlueprintParameter[] components)
    {
        if (components.Length == 0) {
            return "";
        }
        var sb = new StringBuilder();
        sb.Append("<");
        foreach (var component in components) {
            if (sb.Length > 1) {
                sb.Append(", ");
            }
            string type = component.Symbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            sb.Append(type);
        }
        sb.Append(">");
        return sb.ToString();
    }
    
    private static string EmitQueryChunkVariables(BlueprintParameter[] components)
    {
        var sb = new StringBuilder();
        var index = 1;
        foreach (var component in components) {
            var vectorType = component.VectorType;
            // e.g. var componentSpan = chunk.Chunk1.Span;
            if (sb.Length > 0) {
                sb.AppendLine("");
            }
            if (vectorType?.Layout == VectorLayout.SoA) {
                sb.Append($"                var {vectorType.Name}Span = chunk.Chunk{index++}.GetLanesSoA();");
                continue;
            }
            sb.Append($"                var {component.Symbol.Name}Span = chunk.Chunk{index++}.GetComponentSpan();");
        }
        return sb.ToString();
    }
    
    private static string EmitQueryLambdaParameters(Query query)
    {
        var sb = new StringBuilder();
        foreach (var parameter in query.Parameters) {
            var symbol = parameter.Symbol;
            if (sb.Length > 0) {
                sb.Append(", ");
            }
            if (parameter.IsSpan) {
                Utils.AppendRefKind(sb, symbol.RefKind);
                var vectorType = parameter.VectorType;
                if (vectorType?.Layout == VectorLayout.SoA) {
                    sb.Append($"{symbol.Name}AoS");                                                          // TODO fix name SoA
                    continue;
                }
                sb.Append($"{symbol.Name}Span[n]");
                continue;
            }
            if (parameter.IsEntity) {
                sb.Append(query.Spans.Length == 0 ? "entity" : "_entities.EntityAt(n)");
                continue;
            }
            Utils.AppendRefKind(sb, symbol.RefKind);
            sb.Append(symbol.Name);
        }
        return sb.ToString();
    }
    
    private static string EmitQueryFilters(ImmutableArray<AttributeData> attributes)
    {
        var sb = new StringBuilder();
        foreach (var attribute in attributes) {
            var attributeClass = attribute?.AttributeClass;
            if (attributeClass == null) {
                continue;
            }
            string ns = attributeClass.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted));
            if (ns != "Friflo.Engine.ECS") {
                continue;
            }
            switch (attributeClass.Name) {
                case "AllComponentsAttribute":
                case "AnyComponentsAttribute":
                case "WithoutAllComponentsAttribute":
                case "WithoutAnyComponentsAttribute":
                    var name = attributeClass.Name.Substring(0, attributeClass.Name.Length - "Attribute".Length);
                    var args = Utils.GetGenericTypeArguments(attributeClass);
                    sb.AppendLine($"            _query.{name}(ComponentTypes.Get<{args}>());");
                    break;
                case "AllTagsAttribute":
                case "AnyTagsAttribute":
                case "WithoutAllTagsAttribute":
                case "WithoutAnyTagsAttribute":
                    name = attributeClass.Name.Substring(0, attributeClass.Name.Length - "Attribute".Length);
                    args = Utils.GetGenericTypeArguments(attributeClass);
                    sb.AppendLine($"            _query.{name}(Tags.Get<{args}>());");
                    break;
            }
        }
        return sb.ToString();
    }
    
    private static void EmitSoAGetterAndSetter(BlueprintParameter[] components, out StringBuilder getterAoS, out StringBuilder setterAoS)
    {
        getterAoS = new StringBuilder();
        setterAoS = new StringBuilder();
        var index = 1;
        foreach (var component in components) {
            var vectorType = component.VectorType;
            if (vectorType?.Layout == VectorLayout.SoA) {
                getterAoS.AppendLine($"                    var {vectorType.Name}AoS = chunk.Chunk{index}.GetAoSoA(n);");
                if (component.Symbol.RefKind == RefKind.Ref) {
                    setterAoS.AppendLine($"                    chunk.Chunk{index}.SetAoSoA(n, {vectorType.Name}AoS);");
                }
            }
            index++;
        }
    }
}