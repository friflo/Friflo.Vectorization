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
        Diagnostics                     diagnostics,
        out EmissionResult              emissionResult)
    {
        emissionResult = new EmissionResult("", "", diagnostics.List);
        var noEmit          = GeneratorUtils.HasAttribute    (methodAttributes, "Friflo.Vectorization.WebGPU.NoEmitAttribute");
        var shader          = GeneratorUtils.GetAttributeData(methodAttributes, "Friflo.Vectorization.WebGPU.ShaderAttribute");
        var vertexShader    = GeneratorUtils.GetAttributeData(methodAttributes, "Friflo.Vectorization.WebGPU.VertexShaderAttribute");
        var fragmentShader  = GeneratorUtils.GetAttributeData(methodAttributes, "Friflo.Vectorization.WebGPU.FragmentShaderAttribute");

        switch (trigger)
        {
            case  GenerateTrigger.ShaderAttribute:
                if (vertexShader != null || fragmentShader != null) {
                    diagnostics.ReportDiagnosticSymbol(Errors.ShaderError, shader!.AttributeClass, "[Shader] cannot be combined with [VertexShader] or [FragmentShader]");
                    return true;
                }
                break;
            case  GenerateTrigger.VertexShaderAttribute:
                if (fragmentShader == null) {
                    diagnostics.ReportDiagnosticSymbol(Errors.ShaderError, vertexShader!.AttributeClass, "[VertexShader] requires also a [FragmentShader]");
                    return true;
                }
                break;
            case  GenerateTrigger.FragmentShaderAttribute:
                if (vertexShader == null) {
                    diagnostics.ReportDiagnosticSymbol(Errors.ShaderError, fragmentShader!.AttributeClass, "[FragmentShader] requires also a [VertexShader]");
                }
                return true;
            default:
                emissionResult = default;
                return false;
        }
        if (noEmit) {
            return true;
        }
        var method = CreateCsMethod(methodAttributes, methodSymbol);
        var hash = "";
        // var methodSignature = methodSymbol.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);
        // var hash = "_" + GeneratorUtils.GetMd5Hash(methodSignature).Substring(0, 4); // 8 chars is usually enough
        
        var code = ShaderEmitter.EmitShader(methodSymbol.IsStatic, method, hash);

        var fileName = CreateFileName(methodSymbol, hash);
        emissionResult = new EmissionResult(fileName, code, diagnostics.List);
        return true;
    }


    private static CsMethod CreateCsMethod(
        ImmutableArray<AttributeData>   methodAttributes,
        IMethodSymbol                   methodSymbol)
    {
        var declaringType   = MapType(methodSymbol.ContainingType, false);
        var attributes      = methodAttributes.Select(MapAttribute).ToArray();
        
        var methodParameters = methodSymbol.Parameters;
        var parameters  = new CsParameter[methodParameters.Length];
        for (int n = 0; n <  methodParameters.Length; n++)
        {
            var paramSymbol     = methodParameters[n];
            var parameterType   = GetParameterType(paramSymbol, out var attributeData);
            int arg0 = -1;
            int arg1 = -1;
            if (attributeData != null) {
                var args = attributeData.ConstructorArguments;
                switch (parameterType) {
                    case CsParameterType.VertexBuffer:
                        arg0 = (int)args[0].Value!;
                        break;
                    default:
                        arg0 = (int)args[0].Value!;
                        arg1 = (int)args[1].Value!;
                        break;
                }
            }
            parameters[n] = new CsParameter {
                Name            = paramSymbol.Name,
                Type            = MapType(paramSymbol.Type, parameterType != CsParameterType.None),
                ParameterType   = parameterType,
                GroupIndex      = arg0,
                BindingIndex    = arg1
            };
        }
        return new CsMethod {
            Name            = methodSymbol.Name,
            DeclaringType   = declaringType,
            Attributes      = attributes,
            Parameters      = parameters
        };
    }
    
    private static CsParameterType GetParameterType(IParameterSymbol paramSymbol, out AttributeData? attributeData)
    {
        var attributes = paramSymbol.GetAttributes();
        foreach (var attribute in attributes)
        {
            attributeData = attribute;
            var fullName = attribute.AttributeClass!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            switch (fullName)
            {
                case "global::Friflo.Vectorization.WebGPU.VertexBufferAttribute":           return CsParameterType.VertexBuffer;
                //
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
        attributeData = null;
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

        var attributes = typeSymbol.GetAttributes().Select(MapAttribute).ToArray();
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
                    Name        = fieldSymbol.Name,
                    Type        = MapType(fieldSymbol.Type, true), // recursive call
                    Attributes  = fieldSymbol.GetAttributes().Select(MapAttribute).ToArray()
                }).ToArray();
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
