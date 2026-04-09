// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Friflo.Vectorization.Generators;

public partial class AttributeQueryGenerator
{
    private static string EmitQueryMethodSignature(ImmutableArray<IParameterSymbol> parameters, EcsTypes ecsTypes, bool vectorized)
    {
        var sb = new StringBuilder();
        sb.Append("EntityStore _store");
        foreach (var parameter in parameters) {
            bool isComponent = ecsTypes.IsComponent(parameter.Type);
            if (isComponent) {
                continue;
            }
            bool isEntity = ecsTypes.IsEntityParameter(parameter);
            if (isEntity) {
                continue;
            }
            sb.Append(", ");
            Utils.AppendRefKind(sb, parameter.RefKind);
            string type = parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            sb.Append(type);
            sb.Append(" ");
            sb.Append(parameter.Name);
        }
        if (vectorized) {
            sb.Append(", bool vectorized = true");
        }
        return sb.ToString();
    }
    
    private static string EmitQueryArgs(List<IParameterSymbol> components)
    {
        var sb = new StringBuilder();
        foreach (var component in components) {
            if (sb.Length > 0) {
                sb.Append(", ");
            }
            string type = component.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            sb.Append(type);
        }
        return sb.ToString();
    }
    
    private static string EmitQueryChunkVariables(List<IParameterSymbol> components)
    {
        var sb = new StringBuilder();
        var index = 1;
        foreach (var component in components) {
            // e.g. var componentSpan = chunk.Chunk1.Span;
            if (sb.Length > 0) {
                sb.AppendLine("");
            }
            sb.Append("                var ");
            sb.Append(component.Name);
            sb.Append("Span = chunk.Chunk");
            sb.Append(index++);
            sb.Append(".Span;");
        }
        return sb.ToString();
    }
    
    private static string EmitQueryLambdaParameters(ImmutableArray<IParameterSymbol> parameters, EcsTypes ecsTypes)
    {
        var sb = new StringBuilder();
        foreach (var parameter in parameters) {
            if (sb.Length > 0) {
                sb.Append(", ");
            }
            bool isComponent = ecsTypes.IsComponent(parameter.Type);
            if (isComponent) {
                Utils.AppendRefKind(sb, parameter.RefKind);
                sb.Append(parameter.Name);
                sb.Append("Span[n]");
                continue;
            }
            bool isEntity = ecsTypes.IsEntityParameter(parameter); 
            if (isEntity) {
                sb.Append("_entities.EntityAt(n)");
                continue;
            }
            Utils.AppendRefKind(sb, parameter.RefKind);
            sb.Append(parameter.Name);
        }
        return sb.ToString();
    }
    
    private static List<IParameterSymbol> GetQueryComponents(ImmutableArray<IParameterSymbol> parameters, EcsTypes ecsTypes)
    {
        var result = new List<IParameterSymbol>();
        foreach (var parameter in parameters)
        {
            bool isComponent = ecsTypes.IsComponent(parameter.Type);
            if (isComponent) {
                result.Add(parameter);   
            }
        }
        return result;
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
}