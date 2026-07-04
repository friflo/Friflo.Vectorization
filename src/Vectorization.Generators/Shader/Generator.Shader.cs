// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Friflo.Vectorization.Generators;
using Friflo.WGSL.Transpiler.CSharp;
using Microsoft.CodeAnalysis;

// ReSharper disable MergeIntoPattern
// ReSharper disable once CheckNamespace
namespace Friflo;

public sealed partial class ShaderGen
{
    private static string? GenerateShaderMethod(
        ImmutableArray<AttributeData>   methodAttributes,
        IMethodSymbol                   methodSymbol,
        ShaderTrigger                   trigger,
        string                          hash,
        Diagnostics                     diagnostics)
    {
        var noEmit          = GeneratorUtils.HasAttribute    (methodAttributes, "Friflo.Vectorization.WebGPU.NoEmitAttribute");
        var shader          = GeneratorUtils.GetAttributeData(methodAttributes, "Friflo.Vectorization.WebGPU.ShaderAttribute");
        var vertexShader    = GeneratorUtils.GetAttributeData(methodAttributes, "Friflo.Vectorization.WebGPU.VertexShaderAttribute");
        var fragmentShader  = GeneratorUtils.GetAttributeData(methodAttributes, "Friflo.Vectorization.WebGPU.FragmentShaderAttribute");
        //
        var drawVertexIndex = GeneratorUtils.GetAttributeData(methodAttributes, "Friflo.Vectorization.WebGPU.DrawVertexIndexAttribute");

        switch (trigger)
        {
            case  ShaderTrigger.ShaderAttribute:
                if (vertexShader != null || fragmentShader != null) {
                    diagnostics.ReportDiagnosticSymbol(Errors.ShaderError, shader!.AttributeClass, "[Shader] cannot be combined with [VertexShader] or [FragmentShader]");
                    return null;
                }
                break;
            case  ShaderTrigger.VertexShaderAttribute:
                if (fragmentShader == null) {
                    diagnostics.ReportDiagnosticSymbol(Errors.ShaderError, vertexShader!.AttributeClass, "[VertexShader] requires also a [FragmentShader]");
                    return null;
                }
                break;
            case  ShaderTrigger.FragmentShaderAttribute:
                if (vertexShader == null) {
                    diagnostics.ReportDiagnosticSymbol(Errors.ShaderError, fragmentShader!.AttributeClass, "[FragmentShader] requires also a [VertexShader]");
                }
                return null; // only handled by:  ShaderTrigger.VertexShaderAttribute
        }
        if (noEmit) {
            return null;
        }
        var method = CreateCsMethod(methodSymbol, shader, vertexShader, fragmentShader, drawVertexIndex);
        
        var code = ShaderEmitter.EmitShader(methodSymbol.IsStatic, method, hash);

        return code;
    }


    private static CsMethod CreateCsMethod(
        IMethodSymbol   methodSymbol,
        AttributeData?  shader,
        AttributeData?  vertexShader,
        AttributeData?  fragmentShader,
        AttributeData?  drawVertexIndexAttr)
    {
        var declaringType       = MapType(methodSymbol.ContainingType, false);
        var methodParameters    = methodSymbol.Parameters;
        var parameters          = new CsParameter[methodParameters.Length];
        
        for (int n = 0; n <  methodParameters.Length; n++)
        {
            var paramSymbol     = methodParameters[n];
            var attributes      = paramSymbol.GetAttributes();
            var paramAttribute  = GetParamAttribute(attributes, out var attributeData);
            var arg0 = -1;
            var arg1 = -1;
            var sampleType = CsSampleType.None;
            if (attributeData != null) {
                var args = attributeData.ConstructorArguments;
                switch (paramAttribute) {
                    case CsParamAttribute.VertexBuffer:
                        arg0 = (int)args[0].Value!;
                        break;
                    default:
                        arg0 = (int)args[0].Value!;
                        arg1 = (int)args[1].Value!;
                        break;
                }
                var attrTypeArgs = attributeData.AttributeClass!.TypeArguments;
                if (attrTypeArgs.Length > 0) {
                    switch (attrTypeArgs[0].Name) {
                        case "i32": sampleType =  CsSampleType.i32; break;
                        case "u32": sampleType =  CsSampleType.u32; break;
                        case "f32": sampleType =  CsSampleType.f32; break;
                    }
                }
            }
            var drawAttr = GeneratorUtils.GetAttributeData(attributes, "Friflo.Vectorization.WebGPU.DrawAttribute");
            CsDraw? draw = null;
            if (drawAttr != null) {
                var args = drawAttr.ConstructorArguments;
                draw = new CsDraw {
                    instanceCount = (uint)args[0].Value!,
                    firstInstance = (uint)args[1].Value!
                };
            }
            parameters[n] = new CsParameter {
                Name            = paramSymbol.Name,
                Draw            = draw,
                Type            = MapType(paramSymbol.Type, paramAttribute != CsParamAttribute.None),
                ParamAttribute  = paramAttribute,
                BindGroup       = new CsBindGroup {
                    group           = arg0,
                    binding         = arg1
                },
                SampleType      = sampleType
            };
        }
        var vertexEntry   = (string?)(shader != null ? shader.ConstructorArguments[1].Value : vertexShader!  .ConstructorArguments[1].Value);
        var fragmentEntry = (string?)(shader != null ? shader.ConstructorArguments[2].Value : fragmentShader!.ConstructorArguments[1].Value);
        
        CsDrawVertexIndex?  drawVertexIndex = null;
        if (drawVertexIndexAttr != null) {
            var args = drawVertexIndexAttr.ConstructorArguments;
            drawVertexIndex = new CsDrawVertexIndex {
                vertexCount     = (uint)args[0].Value!,
                instanceCount   = (uint)args[1].Value!,
                firstVertex     = (uint)args[2].Value!,
                firstInstance   = (uint)args[3].Value!
            };
        }
        return new CsMethod {
            Name            = methodSymbol.Name,
            DeclaringType   = declaringType,
            Parameters      = parameters,
            Source          = new CsShaderSource {
                Shader          = (string)shader?        .ConstructorArguments[0].Value!,
                VertexShader    = (string)vertexShader?  .ConstructorArguments[0].Value!,
                FragmentShader  = (string)fragmentShader?.ConstructorArguments[0].Value!,
                VertexEntry     = vertexEntry,
                FragmentEntry   = fragmentEntry
            },
            DrawVertexIndex = drawVertexIndex
        };
    }
    
    private static CsParamAttribute GetParamAttribute(ImmutableArray<AttributeData> attributes, out AttributeData? attributeData)
    {
        foreach (var attribute in attributes)
        {
            attributeData = attribute;
            var originalDefinition = attribute.AttributeClass!.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            switch (originalDefinition)
            {
                case "global::Friflo.Vectorization.WebGPU.VertexBufferAttribute":           return CsParamAttribute.VertexBuffer;
                //
                case "global::Friflo.Vectorization.WebGPU.BindStorageAttribute":            return CsParamAttribute.BindStorage;
                case "global::Friflo.Vectorization.WebGPU.BindUniformAttribute":            return CsParamAttribute.BindUniform;
                case "global::Friflo.Vectorization.WebGPU.BindIndexAttribute":              return CsParamAttribute.BindIndex;
                //
                case "global::Friflo.Vectorization.WebGPU.SamplerFiltering":                return CsParamAttribute.SamplerFiltering;
                case "global::Friflo.Vectorization.WebGPU.SamplerNonFiltering":             return CsParamAttribute.SamplerNonFiltering;
                case "global::Friflo.Vectorization.WebGPU.SamplerComparison":               return CsParamAttribute.SamplerComparison;
                //
                case "global::Friflo.Vectorization.WebGPU.texture_1d<ST>":                  return CsParamAttribute.texture_1d;
                case "global::Friflo.Vectorization.WebGPU.texture_2d<ST>":                  return CsParamAttribute.texture_2d;
                case "global::Friflo.Vectorization.WebGPU.texture_2d_array<ST>":            return CsParamAttribute.texture_2d_array;
                case "global::Friflo.Vectorization.WebGPU.texture_3d<ST>":                  return CsParamAttribute.texture_3d;
                case "global::Friflo.Vectorization.WebGPU.texture_cube<ST>":                return CsParamAttribute.texture_cube;
                case "global::Friflo.Vectorization.WebGPU.texture_cube_array<ST>":          return CsParamAttribute.texture_cube_array;
                //
                case "global::Friflo.Vectorization.WebGPU.texture_multisampled_2d<ST>":     return CsParamAttribute.texture_multisampled_2d;
                case "global::Friflo.Vectorization.WebGPU.texture_depth_multisampled_2d":   return CsParamAttribute.texture_depth_multisampled_2d;
                //
                case "global::Friflo.Vectorization.WebGPU.texture_storage_1d<ST>":          return CsParamAttribute.texture_storage_1d;
                case "global::Friflo.Vectorization.WebGPU.texture_storage_2d<ST>":          return CsParamAttribute.texture_storage_2d;
                case "global::Friflo.Vectorization.WebGPU.texture_storage_2d_array<ST>":    return CsParamAttribute.texture_storage_2d_array;
                case "global::Friflo.Vectorization.WebGPU.texture_storage_3d<ST>":          return CsParamAttribute.texture_storage_3d;
                //
                case "global::Friflo.Vectorization.WebGPU.texture_depth_2d":                return CsParamAttribute.texture_depth_2d;
                case "global::Friflo.Vectorization.WebGPU.texture_depth_2d_array":          return CsParamAttribute.texture_depth_2d_array;
                case "global::Friflo.Vectorization.WebGPU.texture_depth_cube":              return CsParamAttribute.texture_depth_cube;
                case "global::Friflo.Vectorization.WebGPU.texture_depth_cube_array":        return CsParamAttribute.texture_depth_cube_array;
            }
        }
        attributeData = null;
        return CsParamAttribute.None;
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
        // NamedArguments required for [StructLayout()] & [FieldOffset()]
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
