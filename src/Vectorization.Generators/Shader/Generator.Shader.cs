// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Friflo.Vectorization.Generators;
using Friflo.WGSL.Transpiler.CSharp;
using Microsoft.CodeAnalysis;
using static Friflo.WGSL.Transpiler.CSharp.CsParamAttribute;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable InvertIf
// ReSharper disable UseCollectionExpression
// ReSharper disable MergeIntoPattern
// ReSharper disable once CheckNamespace
namespace Friflo;

    
public sealed partial class ShaderGen
{

    private static ShaderMethodResult? CreateShaderMethod(
        ImmutableArray<AttributeData>   methodAttributes,
        IMethodSymbol                   methodSymbol,
        string                          hash,
        Diagnostics                     diagnostics)
    {
        var noEmit = GeneratorUtils.HasAttribute(methodAttributes, "Friflo.Vectorization.WebGPU.NoEmitAttribute");
        if (noEmit) {
            return null;
        }
        var shaderAttributes = GeneratorUtils.GetAttributeDatas(methodAttributes, "Friflo.Vectorization.WebGPU.ShaderAttribute");
        //
        var drawVertexIndex = GeneratorUtils.GetAttributeData(methodAttributes, "Friflo.Vectorization.WebGPU.DrawVertexIndexAttribute");

        var method      = CreateCsMethod(methodSymbol, hash, shaderAttributes,  drawVertexIndex, diagnostics);
        
        var fileName    = GeneratorUtils.CreateFileName(methodSymbol, hash);
        var location    = methodSymbol.Locations.FirstOrDefault();

        return new ShaderMethodResult(fileName, method, location, diagnostics.List);
    }


    private static CsMethod CreateCsMethod(
        IMethodSymbol       methodSymbol,
        string              hash,
        List<AttributeData> shaderAttributes,
        AttributeData?      drawVertexIndexAttr,
        Diagnostics         diagnostics)
    {
        var types               = new Dictionary<CsTypeIdentifier, CsTypeInfo>();
        var declaringType       = MapType(types, methodSymbol.ContainingType, false);
        var methodParameters    = methodSymbol.Parameters;
        var parameters          = new CsParameter    [methodParameters.Length];
        var paramModifiers      = new CsParamModifier[methodParameters.Length];
        
        for (int n = 0; n <  methodParameters.Length; n++)
        {
            var paramSymbol     = methodParameters[n];
            var attributes      = paramSymbol.GetAttributes();
            var paramAttribute  = GetParamAttribute(attributes, out var bindGroup, out var e1, out var e2);
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
                Type            = MapType(types, paramSymbol.Type, paramAttribute != None),
                ParamAttribute  = paramAttribute,
                BindGroup       = bindGroup,
                AttrEnum = new CsAttrEnum {
                    enum1           = e1,
                    enum2           = e2,
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
        
        var shaders = new CsShader[shaderAttributes.Count];
        for (int i = 0; i < shaderAttributes.Count; i++)
        {
            var shader = shaderAttributes[i];
            var args = shader.ConstructorArguments;
            var path = (string?)args[0].Value;
            if (path != null) {
                if (path.StartsWith("~/")) path = path.Substring(2);    
            } else {
                diagnostics.ReportDiagnosticSymbol(Errors.ShaderError, shader.AttributeClass, "Expect shader path");
                path = "";
            }
            shaders[i] = new CsShader {
                path = path,
                vert = (string)args[1].Value!,
                frag = (string)args[2].Value!,
            };
        }
        
        return new CsMethod {
            Name            = methodSymbol.Name,
            Hash            = hash, 
            DeclaringType   = declaringType,
            Parameters      = parameters.ToValueArray(),
            Shaders         = shaders.ToValueArray(),
            DrawVertexIndex = drawVertexIndex,
            TypeInfos       = types.Values.ToValueArray(), 
            Modifier        = modifier
        };
    }
    

    
    private static CsEnum Enum(TypedConstant typedConstant)
    {
        if (typedConstant.Kind == TypedConstantKind.Enum && typedConstant.Type is INamedTypeSymbol enumType) {
            var field = enumType.GetMembers()
                .OfType<IFieldSymbol>()
                .FirstOrDefault(f => f.HasConstantValue && f.ConstantValue.Equals(typedConstant.Value));
            if (field != null) return new CsEnum { Name = field.Name, Value = (ulong)(int)typedConstant.Value! };
        }
        return new CsEnum { Name = "NoName", Value = (ulong)(int)typedConstant.Value! };
    }
    
    private static CsBindGroup Int(TypedConstant arg) {
        return new CsBindGroup {
            group   = (int)arg.Value!,
            binding = 0
        };
    }
    
    private static CsBindGroup Bg(ImmutableArray<TypedConstant> args, int pos) {
        return new CsBindGroup {
            group   = (int)args[pos + 0].Value!,
            binding = (int)args[pos + 1].Value!
        };
    }
    
    private static CsParamAttribute GetParamAttribute(
        ImmutableArray<AttributeData>   attributes,
        out CsBindGroup                 bg,
        out CsEnum                      e1,
        out CsEnum                      e2)
    {
        bg = default;
        e1 = default;
        e2 = default;
        CsParamAttribute attr = default;
        
        foreach (var attribute in attributes)
        {
            var symbol = attribute.AttributeClass!.OriginalDefinition;
            if (symbol.ContainingNamespace == null || symbol.ContainingNamespace.IsGlobalNamespace) {
                continue;
            }
            var ns = symbol.ContainingNamespace.ToDisplayString();
            if (ns != "Friflo.Vectorization.WebGPU") {
                continue;   
            }
            var args    = attribute.ConstructorArguments;
                
            switch (symbol.Name)
            {
                case "BindAttribute":           bg = Bg(args, 0);                           continue;
                //
                case "VertexBufferAttribute":   bg = Int(args[0]);  attr = VertexBuffer;    continue;
                //
                case "StorageAttribute":                attr = BindStorage;         continue;
                case "UniformAttribute":                attr = BindUniform;         continue;
                case "BindIndexAttribute":              attr = BindIndex;           continue;
                //
                case "SamplerFilteringAttribute":       attr = SamplerFiltering;    continue;
                case "SamplerNonFilteringAttribute":    attr = SamplerNonFiltering; continue;
                case "SamplerComparisonAttribute":      attr = SamplerComparison;   continue;
                //
                case "texture_1d":                  e1 = Enum(args[0]);     attr = texture_1d;          continue;
                case "texture_2d":                  e1 = Enum(args[0]);     attr = texture_2d;          continue;
                case "texture_2d_array":            e1 = Enum(args[0]);     attr = texture_2d_array;    continue;
                case "texture_3d":                  e1 = Enum(args[0]);     attr = texture_3d;          continue;
                case "texture_cube":                e1 = Enum(args[0]);     attr = texture_cube;        continue;
                case "texture_cube_array":          e1 = Enum(args[0]);     attr = texture_cube_array;  continue;
                //
                case "texture_multisampled_2d":     e1 = Enum(args[0]);     attr = texture_multisampled_2d;         continue;
                case "texture_depth_multisampled_2d":                       attr = texture_depth_multisampled_2d;   continue;
                //
                case "texture_storage_1d":          e1 = Enum(args[0]); e2 = Enum(args[1]); attr = texture_storage_1d;      continue;
                case "texture_storage_2d":          e1 = Enum(args[0]); e2 = Enum(args[1]); attr = texture_storage_2d;      continue;
                case "texture_storage_2d_array":    e1 = Enum(args[0]); e2 = Enum(args[1]); attr = texture_storage_2d_array;continue;
                case "texture_storage_3d":          e1 = Enum(args[0]); e2 = Enum(args[1]); attr = texture_storage_3d;      continue;
                //
                case "texture_depth_2d":            attr = texture_depth_2d;            continue;
                case "texture_depth_2d_array":      attr = texture_depth_2d_array;      continue;
                case "texture_depth_cube":          attr = texture_depth_cube;          continue;
                case "texture_depth_cube_array":    attr = texture_depth_cube_array;    continue;
                default:
                    int i = 111;
                    break;
            }
        }
        return attr;
    }

    private static CsType MapType(Dictionary<CsTypeIdentifier, CsTypeInfo> types, ITypeSymbol typeSymbol, bool getFields)
    {
        var type = GetIdentifier(typeSymbol);
        if (getFields)
        {
            if (!types.ContainsKey(type))
            {
                var attributes = typeSymbol.GetAttributes().Select(MapAttribute).ToArray();
                var typeInfo = new CsTypeInfo {
                    Identifier  = GetIdentifier(typeSymbol),
                    Attributes  = attributes.ToValueArray(),
                    Fields      = []
                };
                // recursion only for struct types
                if (typeSymbol.IsValueType && typeSymbol is INamedTypeSymbol structSymbol)
                {
                    typeInfo.Fields = structSymbol.GetMembers()
                        .OfType<IFieldSymbol>()
                        .Where(fieldSymbol => !fieldSymbol.IsStatic)
                        .Select(fieldSymbol => new CsField
                        {
                            Name        = fieldSymbol.Name,
                            Type        = MapType(types, fieldSymbol.Type, true), // recursive call
                            Attributes  = fieldSymbol.GetAttributes().Select(MapAttribute).ToValueArray()
                        }).ToValueArray();
                }
                types.Add(type, typeInfo);
            }
        }
        
        var genericIdentifiers = new List<CsType>();
        if (typeSymbol is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            foreach (var typeArg in namedType.TypeArguments) {
                var identifier = GetIdentifier(typeArg);
                genericIdentifiers.Add(new CsType {
                    Name        = identifier.Name,
                    Namespace   = identifier.Namespace,
                    Generics    = default
                });
            }
        }
        return new CsType {
            Name        = type.Name,
            Namespace   = type.Namespace,
            Generics    = genericIdentifiers.ToValueArray()
        };
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
            Type = GetIdentifier(attributeData.AttributeClass),
            Args = args.ToValueArray()
        };
    }
    
    private static CsTypeIdentifier GetIdentifier(ITypeSymbol? symbol)
    {
        if (symbol != null)
        {
            var knownType = symbol.SpecialType switch {
                SpecialType.System_Boolean  => "bool",
                SpecialType.System_Char     => "char",
                //
                SpecialType.System_Byte     => "byte",
                SpecialType.System_SByte    => "sbyte",
                SpecialType.System_Int16    => "short",
                SpecialType.System_UInt16   => "ushort",
                SpecialType.System_Int32    => "int",
                SpecialType.System_UInt32   => "uint",
                SpecialType.System_Int64    => "long",
                SpecialType.System_UInt64   => "ulong",
                //
                SpecialType.System_Single   => "float",
                SpecialType.System_Double   => "double",
                _                           => null
            };
            if (knownType != null) {
                return new CsTypeIdentifier {
                    Name        = knownType,
                    Namespace   = ""
                };
            }
        }
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
