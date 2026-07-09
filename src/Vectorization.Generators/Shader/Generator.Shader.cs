// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Friflo.Vectorization.Generators;
using Friflo.WGSL.Transpiler.CSharp;
using Microsoft.CodeAnalysis;

// ReSharper disable UseCollectionExpression
// ReSharper disable MergeIntoPattern
// ReSharper disable once CheckNamespace
namespace Friflo;

    
public sealed partial class ShaderGen
{

    private static ShaderMethodResult? CreateShaderMethod(
        ImmutableArray<AttributeData>   methodAttributes,
        IMethodSymbol                   methodSymbol,
        ShaderTrigger                   trigger,
        string                          hash,
        Diagnostics                     diagnostics)
    {
        var noEmit          = GeneratorUtils.HasAttribute    (methodAttributes, "Friflo.Vectorization.WebGPU.NoEmitAttribute");
        if (noEmit) {
            return null;
        }
        var shader          = GeneratorUtils.GetAttributeData(methodAttributes, "Friflo.Vectorization.WebGPU.ShaderAttribute");
        var vertexShader    = GeneratorUtils.GetAttributeData(methodAttributes, "Friflo.Vectorization.WebGPU.VertexShaderAttribute");
        var fragmentShader  = GeneratorUtils.GetAttributeData(methodAttributes, "Friflo.Vectorization.WebGPU.FragmentShaderAttribute");
        //
        var drawVertexIndex = GeneratorUtils.GetAttributeData(methodAttributes, "Friflo.Vectorization.WebGPU.DrawVertexIndexAttribute");

        switch (trigger)
        {
            case  ShaderTrigger.ShaderAttribute:
                if (vertexShader != null || fragmentShader != null) {
                    diagnostics.ReportDiagnosticSymbol(Errors.ShaderError, methodSymbol, "[Shader] cannot be combined with [VertexShader] or [FragmentShader]");
                    return null;
                }
                break;
            case  ShaderTrigger.VertexShaderAttribute:
                break;
            case  ShaderTrigger.FragmentShaderAttribute:
                if (vertexShader != null) {
                    return null; // only handled by:  ShaderTrigger.VertexShaderAttribute
                }
                break;
        }

        var method = CreateCsMethod(methodSymbol, hash, shader, vertexShader, fragmentShader, drawVertexIndex);
        var fileName = GeneratorUtils.CreateFileName(methodSymbol, hash);

        return new ShaderMethodResult(fileName, method, diagnostics.List);
    }


    private static CsMethod CreateCsMethod(
        IMethodSymbol   methodSymbol,
        string          hash,
        AttributeData?  shader,
        AttributeData?  vertexShader,
        AttributeData?  fragmentShader,
        AttributeData?  drawVertexIndexAttr)
    {
        var declaringType       = MapType(methodSymbol.ContainingType, false);
        var methodParameters    = methodSymbol.Parameters;
        var parameters          = new CsParameter    [methodParameters.Length];
        var paramModifiers      = new CsParamModifier[methodParameters.Length];
        
        for (int n = 0; n <  methodParameters.Length; n++)
        {
            var paramSymbol     = methodParameters[n];
            var attributes      = paramSymbol.GetAttributes();
            var paramAttribute  = GetParamAttribute(attributes, out var attributeData);
            var arg0 = -1;
            var arg1 = -1;
            CsEnum enum1 = default;
            CsEnum enum2 = default;
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
                if (args.Length > 2) {
                    enum1 = GetEnumValue(args[2]);
                }
                if (args.Length > 3) {
                    enum2 = GetEnumValue(args[3]);
                }
            }
            var drawType = CsDrawType.None;
            if (GeneratorUtils.HasAttribute(attributes, "Friflo.Vectorization.WebGPU.DrawAttribute")) {
                drawType = CsDrawType.Draw;
            }
            if (GeneratorUtils.HasAttribute(attributes, "Friflo.Vectorization.WebGPU.DrawInstanceAttribute")) {
                drawType = CsDrawType.DrawInstance;
            }
            if (GeneratorUtils.HasAttribute(attributes, "Friflo.Vectorization.WebGPU.DrawFirstVertexAttribute")) {
                drawType = CsDrawType.DrawFirstVertex;
            }
            if (GeneratorUtils.HasAttribute(attributes, "Friflo.Vectorization.WebGPU.DrawFirstInstanceAttribute")) {
                drawType = CsDrawType.DrawFirstInstance;
            }
            parameters[n] = new CsParameter {
                Name            = paramSymbol.Name,
                DrawType        = drawType,
                Type            = MapType(paramSymbol.Type, paramAttribute != CsParamAttribute.None),
                ParamAttribute  = paramAttribute,
                BindGroup       = new CsBindGroup {
                    group           = arg0,
                    binding         = arg1
                },
                AttrEnum = new CsAttrEnum {
                    enum1           = enum1,
                    enum2           = enum2,
                }
            };
            var modifierType = paramSymbol.RefKind switch {
                RefKind.In  => "in ",
                RefKind.Out => "out ",
                RefKind.Ref => "ref ",
                _ => ""
            };
            paramModifiers[n] = new CsParamModifier { type = modifierType };
        }
        var vertexEntry   = (string?)(shader?.ConstructorArguments[1].Value ?? vertexShader?  .ConstructorArguments[1].Value);
        var fragmentEntry = (string?)(shader?.ConstructorArguments[2].Value ?? fragmentShader?.ConstructorArguments[1].Value);
        
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
        var modifier = CreateMethodModifier(methodSymbol, paramModifiers);
        
        return new CsMethod {
            Name            = methodSymbol.Name,
            Hash            = hash, 
            DeclaringType   = declaringType,
            Parameters      = parameters.ToValueArray(),
            Source          = new CsShaderSource {
                Shader          = (string)shader?        .ConstructorArguments[0].Value!,
                VertexShader    = (string)vertexShader?  .ConstructorArguments[0].Value!,
                FragmentShader  = (string)fragmentShader?.ConstructorArguments[0].Value!,
                VertexEntry     = vertexEntry,
                FragmentEntry   = fragmentEntry
            },
            DrawVertexIndex = drawVertexIndex,
            Modifier        = modifier
        };
    }
    
    private static CsEnum GetEnumValue(TypedConstant typedConstant)
    {
        if (typedConstant.Kind == TypedConstantKind.Enum && typedConstant.Type is INamedTypeSymbol enumType) {
            var field = enumType.GetMembers()
                .OfType<IFieldSymbol>()
                .FirstOrDefault(f => f.HasConstantValue && f.ConstantValue.Equals(typedConstant.Value));
            if (field != null) return new CsEnum { Name = field.Name, Value = (ulong)(int)typedConstant.Value! };
        }
        return new CsEnum { Name = "NoName", Value = (ulong)(int)typedConstant.Value! };
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
                case "global::Friflo.Vectorization.WebGPU.SamplerFilteringAttribute":       return CsParamAttribute.SamplerFiltering;
                case "global::Friflo.Vectorization.WebGPU.SamplerNonFilteringAttribute":    return CsParamAttribute.SamplerNonFiltering;
                case "global::Friflo.Vectorization.WebGPU.SamplerComparisonAttribute":      return CsParamAttribute.SamplerComparison;
                //
                case "global::Friflo.Vectorization.WebGPU.texture_1d":                      return CsParamAttribute.texture_1d;
                case "global::Friflo.Vectorization.WebGPU.texture_2d":                      return CsParamAttribute.texture_2d;
                case "global::Friflo.Vectorization.WebGPU.texture_2d_array":                return CsParamAttribute.texture_2d_array;
                case "global::Friflo.Vectorization.WebGPU.texture_3d":                      return CsParamAttribute.texture_3d;
                case "global::Friflo.Vectorization.WebGPU.texture_cube":                    return CsParamAttribute.texture_cube;
                case "global::Friflo.Vectorization.WebGPU.texture_cube_array":              return CsParamAttribute.texture_cube_array;
                //
                case "global::Friflo.Vectorization.WebGPU.texture_multisampled_2d":         return CsParamAttribute.texture_multisampled_2d;
                case "global::Friflo.Vectorization.WebGPU.texture_depth_multisampled_2d":   return CsParamAttribute.texture_depth_multisampled_2d;
                //
                case "global::Friflo.Vectorization.WebGPU.texture_storage_1d":              return CsParamAttribute.texture_storage_1d;
                case "global::Friflo.Vectorization.WebGPU.texture_storage_2d":              return CsParamAttribute.texture_storage_2d;
                case "global::Friflo.Vectorization.WebGPU.texture_storage_2d_array":        return CsParamAttribute.texture_storage_2d_array;
                case "global::Friflo.Vectorization.WebGPU.texture_storage_3d":              return CsParamAttribute.texture_storage_3d;
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
            Generics    = genericIdentifiers.ToValueArray(),
            Attributes  = attributes.ToValueArray(),
            Fields      = []
        };
        if (!getFields) {
            return csType;
        }

        // recursion only for struct types
        if (typeSymbol.IsValueType && typeSymbol is INamedTypeSymbol structSymbol)
        {
            csType.Fields = structSymbol.GetMembers()
                .OfType<IFieldSymbol>()
                .Where(fieldSymbol => !fieldSymbol.IsStatic)
                .Select(fieldSymbol => new CsField
                {
                    Name        = fieldSymbol.Name,
                    Type        = MapType(fieldSymbol.Type, true), // recursive call
                    Attributes  = fieldSymbol.GetAttributes().Select(MapAttribute).ToValueArray()
                }).ToValueArray();
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
            Args = args.ToValueArray()
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
    
    private static CsModifier CreateMethodModifier(IMethodSymbol methodSymbol, CsParamModifier[] paramModifiers)
    {
        var containingType  = methodSymbol.ContainingType;
        var visibility      = methodSymbol.DeclaredAccessibility switch {
            Accessibility.Private               => "private",
            Accessibility.Protected             => "protected",
            Accessibility.Public                => "public",
            Accessibility.Internal              => "internal",
            Accessibility.ProtectedAndInternal  => "protected internal",
            _                                   => ""
        };
        return new CsModifier {
            IsClass             = !containingType.IsValueType,
            IsMethodStatic      = methodSymbol.IsStatic,
            MethodVisibility    = visibility,
            ParamModifiers      = paramModifiers.ToValueArray()
        };
    }
}
