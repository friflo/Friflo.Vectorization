// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Text;
using Friflo.Vectorization.Generators;
using Friflo.Vectorization.Generators.AVX;
using Microsoft.CodeAnalysis;

// ReSharper disable once CheckNamespace
// Note: Used small namespace and class name to enable shorter path names in 'Generated' folders
namespace Friflo;

public sealed partial class Gen
{
    private static void EmitQuerySource(
        Query query,
        out string shadowMethodSource,
        out string privateSource)
    {
        var attributeCode       = EmitQueryFilters(query.Attributes);
        var lambdaParameters    = EmitQueryLambdaParameters(query);
        var methodSignature     = EmitQueryMethodSignature(query, query.vectorized);
        var vectorizeBlock      = AvxVectorizer.EmitVectorizeBlock(query);
        
        var components = query.Spans;
        var componentArgs   = new StringBuilder();
        
        var chunkVariables  = new StringBuilder();
        var chunkIndex      = 1;
        var getterAoS       = new StringBuilder();
        var setterAoS       = new StringBuilder();
        
        if (components.Length > 0) componentArgs.Append("<");
        foreach (var component in components) {
            // --- componentArgs
            if (componentArgs.Length > 1) {
                componentArgs.Append(", ");
            }
            string type = component.TypeName;
            componentArgs.Append(type);
            
            // --- chunkVariables
            var vectorType = component.VectorType;
            // e.g. var componentSpan = chunk.Chunk1.Span;
            if (chunkVariables.Length > 0) {
                chunkVariables.AppendLine("");
            }
            chunkVariables.Append(vectorType?.Layout == VectorLayout.AoSoA
                ? $"                var {component.Name}Span = chunk.Chunk{chunkIndex}.GetLanesSoA();"
                : $"                var {component.Name}Span = chunk.Chunk{chunkIndex}.GetComponentSpan();");

            // --- getterAoS / setterAoS
            if (vectorType?.Layout == VectorLayout.AoSoA) {
                getterAoS.AppendLine($"                    var {vectorType.Name}AoS = chunk.Chunk{chunkIndex}.GetAoSoA(n);");
                if (component.RefKind == RefKind.Ref) {
                    setterAoS.AppendLine($"                    chunk.Chunk{chunkIndex}.SetAoSoA(n, {vectorType.Name}AoS);");
                }
            }
            chunkIndex++;
        }
        if (components.Length > 0) componentArgs.Append(">");
        
        var hash            = query.Hash;
        var blueprintMethod = query.BlueprintMethod;
        var methodName      = blueprintMethod.Name;
        
        if (query.Spans.Length == 0)
        {
            shadowMethodSource =   
            $$"""
            
                    /// <summary>Query method generated for: <see cref="{{methodName}}"/>.</summary>
                    /// <returns>The executed <see cref="ArchetypeQuery"/> for debugging purposes</returns>
                    public {{(blueprintMethod.IsStatic ? "static " : "")}}ArchetypeQuery {{methodName}}Query({{methodSignature}})
                    {
                        var _query = _{{methodName}}_GetQuery{{hash}}(_store);
                        foreach (var entity in _query.Entities)
                        {
                            {{methodName}}({{lambdaParameters}});
                        }
                        return _query;
                    }
            """;
        } else {
            shadowMethodSource =
            $$"""
            
                    /// <summary>Query method generated for: <see cref="{{methodName}}"/>.</summary>
                    /// <returns>The executed <see cref="ArchetypeQuery"/> for debugging purposes</returns>
                    public {{(blueprintMethod.IsStatic ? "static " : "")}}ArchetypeQuery {{methodName}}Query({{methodSignature}})
                    {
                        var _query = _{{methodName}}_GetQuery{{hash}}(_store);
                        foreach (var chunk in _query.Chunks)
                        {
                            var _entities = chunk.Entities;
            {{chunkVariables}}
                            int n = 0;{{vectorizeBlock}}
                            for (; n < _entities.Length; n++) {
            {{getterAoS}}                    {{methodName}}({{lambdaParameters}});
            {{setterAoS}}                }
                        }
                        return _query;
                    }
            """;
        }
        
        privateSource =
        $$"""
        
                [EditorBrowsable(EditorBrowsableState.Never)]
                private static readonly int _{{methodName}}_Slot{{hash}} = EntityStore.UserDataNewSlot();
        
                [EditorBrowsable(EditorBrowsableState.Never)]
                private static ArchetypeQuery{{componentArgs}}
                    _{{methodName}}_GetQuery{{hash}}(EntityStore _store)
                {
                    var _query = (ArchetypeQuery{{componentArgs}})
                        EntityStore.UserDataGet(_store, _{{methodName}}_Slot{{hash}});
                    if (_query != null) {
                        return _query;
                    }
                    _query = _store.Query{{componentArgs}}();
        {{attributeCode}}
                    EntityStore.UserDataSet(_store, _{{methodName}}_Slot{{hash}}, _query);
                    return _query;
                }
        """;
    }
    
    private static StringBuilder EmitQueryMethodSignature(Query query, bool vectorized)
    {
        var sb = new StringBuilder();
        sb.Append("EntityStore _store");
        var parameters = query.Parameters;
        foreach (var parameter in parameters) {
            if (parameter.IsSpan) {
                continue;
            }
            if (parameter.IsEntity) {
                continue;
            }
            sb.Append(", ");
            GeneratorUtils.AppendRefKind(sb, parameter.RefKind);
            sb.Append(parameter.TypeName);
            sb.Append(" ");
            sb.Append(parameter.Name);
        }
        if (vectorized) {
            sb.Append(", bool vectorized = true");
        }
        return sb;
    }
    
    private static StringBuilder EmitQueryLambdaParameters(Query query)
    {
        var sb = new StringBuilder();
        foreach (var parameter in query.Parameters) {
            if (sb.Length > 0) {
                sb.Append(", ");
            }
            if (parameter.IsSpan) {
                GeneratorUtils.AppendRefKind(sb, parameter.RefKind);
                var vectorType = parameter.VectorType;
                if (vectorType?.Layout == VectorLayout.AoSoA) {
                    sb.Append($"{parameter.Name}AoS");                                                          // TODO fix name SoA
                    continue;
                }
                sb.Append($"{parameter.Name}Span[n]");
                continue;
            }
            if (parameter.IsEntity) {
                sb.Append(query.Spans.Length == 0 ? "entity" : "_entities.EntityAt(n)");
                continue;
            }
            GeneratorUtils.AppendRefKind(sb, parameter.RefKind);
            sb.Append(parameter.Name);
        }
        return sb;
    }
    
    private static StringBuilder EmitQueryFilters(ImmutableArray<AttributeData> attributes)
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
                    var args = GeneratorUtils.GetGenericTypeArguments(attributeClass);
                    sb.AppendLine($"            _query.{name}(ComponentTypes.Get<{args}>());");
                    break;
                case "AllTagsAttribute":
                case "AnyTagsAttribute":
                case "WithoutAllTagsAttribute":
                case "WithoutAnyTagsAttribute":
                    name = attributeClass.Name.Substring(0, attributeClass.Name.Length - "Attribute".Length);
                    args = GeneratorUtils.GetGenericTypeArguments(attributeClass);
                    sb.AppendLine($"            _query.{name}(Tags.Get<{args}>());");
                    break;
            }
        }
        return sb;
    }
}