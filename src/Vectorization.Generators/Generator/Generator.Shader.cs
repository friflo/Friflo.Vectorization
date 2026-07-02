// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Friflo.Vectorization.Generators;
using Friflo.WGSL.Transpiler.CSharp;
using Microsoft.CodeAnalysis;


// ReSharper disable once CheckNamespace
namespace Friflo;

public sealed partial class Gen
{
    private static bool GenerateShaderMethod(
        ImmutableArray<AttributeData>   methodAttributes,
        IMethodSymbol                   methodSymbol,
        GenerateTrigger                 trigger,
        out EmissionResult              emissionResult)
    {
        var shader          = GeneratorUtils.GetAttributeData(methodAttributes, "Friflo.Vectorization.WebGPU.ShaderAttribute");
        var vertexShader    = GeneratorUtils.GetAttributeData(methodAttributes, "Friflo.Vectorization.WebGPU.VertexShaderAttribute");
        var fragmentShader  = GeneratorUtils.GetAttributeData(methodAttributes, "Friflo.Vectorization.WebGPU.FragmentShaderAttribute");
        
        if (shader == null && vertexShader == null && fragmentShader == null) {
            emissionResult = default;
            return false;
        }
        var diagnostics = new Diagnostics { BlueprintMethod = methodSymbol };
        if (shader != null) {
            if (vertexShader != null || fragmentShader != null) {
                diagnostics.ReportDiagnosticSymbol(Errors.ShaderError, shader.AttributeClass, "[Shader] cannot be combined with [VertexShader] or [FragmentShader]");
                emissionResult = new EmissionResult("", "", diagnostics.List);
                return false;
            }
        }
        // CreateCsMethod(methodAttributes, methodSymbol);
        emissionResult = new EmissionResult("", "", diagnostics.List);
        return true;
    }
    


    private static CsMethod CreateCsMethod(
        ImmutableArray<AttributeData>   methodAttributes,
        IMethodSymbol                   methodSymbol)
    {
        return new CsMethod
        {
            Identifier = new CsTypeIdentifier
            {
                Name = methodSymbol.Name,
                Namespace = methodSymbol.ContainingType?.ContainingNamespace?.IsGlobalNamespace == false
                    ? methodSymbol.ContainingType.ContainingNamespace.ToDisplayString()
                    : string.Empty
            },

            Attributes = methodAttributes.Select(MapAttribute).ToList(),

            Parameters = methodSymbol.Parameters.Select(paramSymbol => new CsParameter
            {
                Name = paramSymbol.Name,
                Type = MapType(paramSymbol.Type),
                Attributes = paramSymbol.GetAttributes().Select(MapAttribute).ToList()
            }).ToList()
        };
    }

    private static CsType MapType(ITypeSymbol typeSymbol)
    {
        var genericIdentifiers = new List<CsTypeIdentifier>();
        string typeName = typeSymbol.Name;

        if (typeSymbol is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            typeName = namedType.Name;
            
            foreach (var typeArg in namedType.TypeArguments)
            {
                genericIdentifiers.Add(new CsTypeIdentifier
                {
                    Name = typeArg.Name,
                    Namespace = typeArg.ContainingNamespace?.IsGlobalNamespace == false
                        ? typeArg.ContainingNamespace.ToDisplayString()
                        : string.Empty
                });
            }
        }

        var csType = new CsType
        {
            Identifier = new CsTypeIdentifier
            {
                Name = typeName,
                Namespace = typeSymbol.ContainingNamespace?.IsGlobalNamespace == false
                    ? typeSymbol.ContainingNamespace.ToDisplayString()
                    : string.Empty
            },
            Kind = typeSymbol.IsValueType ? CsTypeKind.Struct : CsTypeKind.Class,
            Generics = genericIdentifiers,
            Attributes = typeSymbol.GetAttributes().Select(MapAttribute).ToList(),
            Fields = []
        };

        // recursion only for struct types
        if (csType.Kind == CsTypeKind.Struct && typeSymbol is INamedTypeSymbol structSymbol)
        {
            csType.Fields = structSymbol.GetMembers()
                .OfType<IFieldSymbol>()
                .Where(fieldSymbol => !fieldSymbol.IsStatic)
                .Select(fieldSymbol => new CsField
                {
                    Name = fieldSymbol.Name,
                    Type = MapType(fieldSymbol.Type), // recursive call
                    Attributes = fieldSymbol.GetAttributes().Select(MapAttribute).ToList()
                }).ToList();
        }
        return csType;
    }

    private static CsAttribute MapAttribute(AttributeData attributeData)
    {
        var args = new List<CsAttributeArg>();

        foreach (var constructorArg in attributeData.ConstructorArguments)
        {
            args.Add(new CsAttributeArg
            {
                Name = string.Empty,
                Value = constructorArg.Value?.ToString() ?? "null"
            });
        }

        foreach (var namedArg in attributeData.NamedArguments)
        {
            args.Add(new CsAttributeArg
            {
                Name = namedArg.Key,
                Value = namedArg.Value.Value?.ToString() ?? "null"
            });
        }

        return new CsAttribute
        {
            Identifier = new CsTypeIdentifier
            {
                Name = attributeData.AttributeClass?.Name ?? "UnknownAttribute",
                Namespace = attributeData.AttributeClass?.ContainingNamespace?.IsGlobalNamespace == false
                    ? attributeData.AttributeClass.ContainingNamespace.ToDisplayString()
                    : string.Empty
            },
            Args = args
        };
    }
}
