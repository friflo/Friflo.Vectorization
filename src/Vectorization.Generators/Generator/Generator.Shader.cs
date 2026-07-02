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
        var noEmit          = GeneratorUtils.HasAttribute    (methodAttributes, "Friflo.Vectorization.WebGPU.NoEmitAttribute");
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
        var method = CreateCsMethod(methodAttributes, methodSymbol);
        var hash = "";
        // var methodSignature = methodSymbol.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);
        // var hash = "_" + GeneratorUtils.GetMd5Hash(methodSignature).Substring(0, 4); // 8 chars is usually enough
        
        var code = ShaderEmitter.EmitShader(methodSymbol.IsStatic, method, hash);
        
        if (noEmit) {
            emissionResult = default;
            return false;
        }
        var fileName = CreateFileName(methodSymbol, hash);
        emissionResult = new EmissionResult(fileName, code, diagnostics.List);
        return true;
    }


    private static CsMethod CreateCsMethod(
        ImmutableArray<AttributeData>   methodAttributes,
        IMethodSymbol                   methodSymbol)
    {
        var declaringType   = MapType(methodSymbol.ContainingType, false);
        var attributes      = methodAttributes.Select(MapAttribute).ToList();
        
        var parameters  = new List<CsParameter>();
        foreach (var paramSymbol in methodSymbol.Parameters)
        {
            var parameterType = GetParameterType(paramSymbol);
            parameters.Add(new CsParameter {
                Name            = paramSymbol.Name,
                Type            = MapType(paramSymbol.Type, parameterType != CsParameterType.None),
                ParameterType   = GetParameterType(paramSymbol)
            });
        }
        return new CsMethod {
            Name            = methodSymbol.Name,
            DeclaringType   = declaringType,
            Attributes      = attributes,
            Parameters      = parameters
        };
    }
    
    private static CsParameterType GetParameterType(IParameterSymbol paramSymbol)
    {
        var attributes = paramSymbol.GetAttributes();
        foreach (var attribute in attributes)
        {
            var fullName = attribute.AttributeClass!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            switch (fullName)
            {
                case "global::Friflo.Vectorization.WebGPU.VertexBufferAttribute":           return CsParameterType.VertexBuffer;
                case "global::Friflo.Vectorization.WebGPU.BindStorageAttribute":            return CsParameterType.BindStorage;
                case "global::Friflo.Vectorization.WebGPU.BindUniformAttribute":            return CsParameterType.BindUniform;
                case "global::Friflo.Vectorization.WebGPU.BindIndexAttribute":              return CsParameterType.BindIndex;
                //
                case "global::Friflo.Vectorization.WebGPU.SamplerFiltering":                return CsParameterType.SamplerFiltering;
                case "global::Friflo.Vectorization.WebGPU.SamplerNonFiltering":             return CsParameterType.SamplerNonFiltering;
                case "global::Friflo.Vectorization.WebGPU.SamplerComparison":               return CsParameterType.SamplerComparison;
                //
                case "global::Friflo.Vectorization.WebGPU.texture_1d":                      return CsParameterType.texture_1d;
                case "global::Friflo.Vectorization.WebGPU.texture_2d":                      return CsParameterType.texture_2d;
                case "global::Friflo.Vectorization.WebGPU.texture_2d_array":                return CsParameterType.texture_2d_array;
                case "global::Friflo.Vectorization.WebGPU.texture_3d":                      return CsParameterType.texture_3d;
                case "global::Friflo.Vectorization.WebGPU.texture_cube":                    return CsParameterType.texture_cube;
                case "global::Friflo.Vectorization.WebGPU.texture_cube_array":              return CsParameterType.texture_cube_array;
                case "global::Friflo.Vectorization.WebGPU.texture_multisampled_2d":         return CsParameterType.texture_multisampled_2d;
                case "global::Friflo.Vectorization.WebGPU.texture_depth_multisampled_2d":   return CsParameterType.texture_depth_multisampled_2d;
                case "global::Friflo.Vectorization.WebGPU.texture_storage_1d":              return CsParameterType.texture_storage_1d;
                case "global::Friflo.Vectorization.WebGPU.texture_storage_2d":              return CsParameterType.texture_storage_2d;
                case "global::Friflo.Vectorization.WebGPU.texture_storage_2d_array":        return CsParameterType.texture_storage_2d_array;
                case "global::Friflo.Vectorization.WebGPU.texture_storage_3d":              return CsParameterType.texture_storage_3d;
                case "global::Friflo.Vectorization.WebGPU.texture_depth_2d":                return CsParameterType.texture_depth_2d;
                case "global::Friflo.Vectorization.WebGPU.texture_depth_2d_array":          return CsParameterType.texture_depth_2d_array;
                case "global::Friflo.Vectorization.WebGPU.texture_depth_cube":              return CsParameterType.texture_depth_cube;
                case "global::Friflo.Vectorization.WebGPU.texture_depth_cube_array":        return CsParameterType.texture_depth_cube_array;
            }
        }
        return CsParameterType.None;
    }

    private static CsType MapType(ITypeSymbol typeSymbol, bool getFields)
    {
        var genericIdentifiers = new List<CsTypeIdentifier>();
        if (typeSymbol is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            foreach (var typeArg in namedType.TypeArguments) {
                genericIdentifiers.Add(GetIdentifier(typeArg));
            }
        }

        var attributes = typeSymbol.GetAttributes().Select(MapAttribute).ToList();
        var csType = new CsType {
            Identifier  = GetIdentifier(typeSymbol),
            Kind        = typeSymbol.IsValueType ? CsTypeKind.Struct : CsTypeKind.Class,
            Generics    = genericIdentifiers,
            Attributes  = attributes,
            Fields      = []
        };
        if (!getFields) {
            return csType;
        }

        // recursion only for struct types
        if (csType.Kind == CsTypeKind.Struct && typeSymbol is INamedTypeSymbol structSymbol)
        {
            csType.Fields = structSymbol.GetMembers()
                .OfType<IFieldSymbol>()
                .Where(fieldSymbol => !fieldSymbol.IsStatic)
                .Select(fieldSymbol => new CsField
                {
                    Name = fieldSymbol.Name,
                    Type = MapType(fieldSymbol.Type, true), // recursive call
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
            args.Add(new CsAttributeArg {
                Name    = string.Empty,
                Value   = constructorArg.Value?.ToString() ?? "null"
            });
        }

        foreach (var namedArg in attributeData.NamedArguments)
        {
            args.Add(new CsAttributeArg {
                Name    = namedArg.Key,
                Value   = namedArg.Value.Value?.ToString() ?? "null"
            });
        }
        return new CsAttribute {
            Identifier = GetIdentifier(attributeData.AttributeClass),
            Args = args
        };
    }
    
    private static CsTypeIdentifier GetIdentifier(ITypeSymbol? symbol)
    {
        var ns = symbol?.ContainingNamespace?.IsGlobalNamespace == false
                    ? symbol.ContainingNamespace.ToDisplayString()
                    : string.Empty;
        return new CsTypeIdentifier {
            Name        = symbol?.Name ?? "UnknownType",
            Namespace   = ns
        };
    }
}
