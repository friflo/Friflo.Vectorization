// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Friflo.Vectorization.Generators;
using Friflo.Vectorization.Generators.Shader;
using Friflo.WGSL.Transpiler.CSharp;
using Microsoft.CodeAnalysis;
using static Friflo.WGSL.Transpiler.CSharp.CsParamAttribute;

// ReSharper disable ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable InvertIf
// ReSharper disable UseCollectionExpression
// ReSharper disable MergeIntoPattern
// ReSharper disable once CheckNamespace
namespace Friflo;

    
internal static partial class ShaderGenerator
{
    internal static ShaderMethodResult? CreateShaderMethod(IMethodSymbol methodSymbol, string hash, SemanticModel semanticModel, Diagnostics diagnostics)
    {
        var methodAttributes  = methodSymbol.GetAttributes();

        var noEmit = GeneratorUtils.HasAttribute(methodAttributes, "Friflo.Vectorization.WebGPU.NoEmitAttribute");
        if (noEmit) {
            return null;
        }
        var shaderAttributes    = GeneratorUtils.GetAttributeDatas(methodAttributes, "Friflo.Vectorization.WebGPU.ShaderAttribute");
        var workgroupSize       = GeneratorUtils.GetAttributeData (methodAttributes, "Friflo.Vectorization.WebGPU.WorkgroupSizeAttribute");
        
        var semanticInfo = new SemanticInfo(semanticModel);

        var method      = CreateCsMethod(semanticInfo, methodSymbol, hash, shaderAttributes,  workgroupSize, diagnostics);
        
        var fileName    = GeneratorUtils.CreateFileName(methodSymbol, hash);

        return new ShaderMethodResult(fileName, method, diagnostics.List);
    }


    private static CsMethod CreateCsMethod(
        SemanticInfo        semanticInfo,
        IMethodSymbol       methodSymbol,
        string              hash,
        List<AttributeData> shaderAttributes,
        AttributeData?      workgroupSizeAttr,
        Diagnostics         diagnostics)
    {
        var declaringType       = MapType(semanticInfo, methodSymbol.ContainingType, false);
        var methodParameters    = methodSymbol.Parameters;
        var parameters          = new CsParameter    [methodParameters.Length];
        var paramModifiers      = new CsParamModifier[methodParameters.Length];
        
        for (int n = 0; n <  methodParameters.Length; n++)
        {
            var paramSymbol         = methodParameters[n];
            var attributes          = paramSymbol.GetAttributes();
            var paramAttribute      = GetParamAttribute(attributes, out var bindGroup, out int vbs, out var e1, out var e2, out var attributeData);
            var workloadAttribute   = CsWorkloadAttribute.None;
            if (workgroupSizeAttr == null) {
                if (GeneratorUtils.HasAttribute(attributes, "Friflo.Vectorization.WebGPU.DrawAttribute")) {
                    workloadAttribute = CsWorkloadAttribute.Draw;
                }
                if (GeneratorUtils.HasAttribute(attributes, "Friflo.Vectorization.WebGPU.DrawInstanceAttribute")) {
                    workloadAttribute = CsWorkloadAttribute.DrawInstance;
                }
            } else {
                if (GeneratorUtils.HasAttribute(attributes, "Friflo.Vectorization.WebGPU.DispatchAttribute")) {
                    workloadAttribute   = CsWorkloadAttribute.Dispatch;
                }
            }
            var type = MapType(semanticInfo, paramSymbol.Type, paramAttribute != None);
            var (nameLoc, typeLoc, genericArgLoc) 	= paramSymbol.GetParameterLocs();
            var (attrLoc, arg0Loc, arg1Loc) 		= attributeData.GetParamSrcLocs();
            
            parameters[n] = new CsParameter {
                Name                = paramSymbol.Name,
                WorkloadAttribute   = workloadAttribute,
                Type                = type,
                ParamAttribute      = paramAttribute,
                BindGroup           = bindGroup,
                VertexBufferSlot    = vbs,
                AttrEnum = new CsAttrEnum {
                    enum1               = e1,
                    enum2               = e2,
                },
                NameLoc             = nameLoc,
                TypeLoc             = typeLoc,
                GenericArgLoc       = genericArgLoc,
                AttrLoc             = attrLoc,
                AttrArg0Loc         = arg0Loc,
                AttrArg1Loc         = arg1Loc
            };
            var modifierType = paramSymbol.RefKind switch {
                RefKind.In  => "in ",
                RefKind.Out => "out ",
                RefKind.Ref => "ref ",
                _ => ""
            };
            paramModifiers[n] = new CsParamModifier { type = modifierType };
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
            var (attrLoc, pathLoc, vertLoc, fragLoc, computeLoc) = shader.GetShaderSrcLocs();
            shaders[i] = new CsShader {
                path        = path,
                vert        = (string)args[1].Value!,
                frag        = (string)args[2].Value!,
                compute     = (string)args[3].Value!,
                attrLoc     = attrLoc,
                pathLoc     = pathLoc,
                vertLoc     = vertLoc,
                fragLoc     = fragLoc,
                computeLoc  = computeLoc
            };
        }
        
        var typeInfos = semanticInfo.types.Values.Where(ti => ti.TypeCode > CsTypeCode.None && ti.TypeCode <= CsTypeCode.CSharpStruct).ToValueArray();
        
        CsWorkgroupSize? workgroupSize = workgroupSizeAttr == null ? null : GetWorkgroupSize(workgroupSizeAttr);
        
        return new CsMethod {
            Name            = methodSymbol.Name,
            Hash            = hash, 
            DeclaringType   = declaringType,
            Parameters      = parameters.ToValueArray(),
            Shaders         = shaders.ToValueArray(),
            TypeInfos       = typeInfos, 
            Modifier        = modifier,
            WorkgroupSize   = workgroupSize,
            MethodLoc       = methodSymbol.GetSymbolLoc()
        };
    }


    private static CsWorkgroupSize GetWorkgroupSize(AttributeData dispatchAttrData)
    {
        var args = dispatchAttrData.ConstructorArguments;
        var loc  = dispatchAttrData.GetAttributeLoc();
        return new CsWorkgroupSize {
            workgroupCountX = args[0].Value is int x ? x : 0,
            workgroupCountY = args[1].Value is int y ? y : 0,
            workgroupCountZ = args[2].Value is int z ? z : 0,
            attrLoc         = loc
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
    
    private static int Int(TypedConstant arg) {
        return (int)arg.Value!;
    }
    
    private static CsBindGroup BindGroup(ImmutableArray<TypedConstant> args, int pos, SrcLoc loc) {
        return new CsBindGroup {
            group   = (int)args[pos + 0].Value!,
            binding = (int)args[pos + 1].Value!,
            attrLoc = loc
        };
    }
    
    private static CsParamAttribute GetParamAttribute(
        ImmutableArray<AttributeData>   attributes,
        out CsBindGroup                 bg,
        out int                         vbs,
        out CsEnum                      e1,
        out CsEnum                      e2,
        out AttributeData?              attributeData)
    {
        bg      = default;
        e1      = default;
        e2      = default;
        vbs     = 0;
        CsParamAttribute attr = default;
        attributeData = null;
        
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
            var loc     = attribute.GetAttributeLoc();
            var args    = attribute.ConstructorArguments;
                
            switch (symbol.Name)
            {
                // --- WGSL: bind group ---
                case "MapAttribute":            bg = BindGroup(args, 0, loc);                   continue;
                
                // --- WGSL: Buffer types ---
                case "storageAttribute":                                attr = storage;         break;
                case "uniformAttribute":                                attr = uniform;         break;
                //
                case "VertexBufferAttribute":   vbs = Int(args[0]);     attr = VertexBuffer;    break;
                //
                case "IndexBufferAttribute":                            attr = IndexBuffer;     break;
                
                // --- WGSL: Sampler types ---
                case "samplerAttribute":
                    if ((bool)args[0].Value!)                           attr = sampler;
                    else                                                attr = sampler_NonFiltering; break;
                case "sampler_comparisonAttribute":                     attr = sampler_comparison;   break;

                // --- WGSL: Texture types ---
                case "texture_1dAttribute":                 e1 = Enum(args[0]); attr = texture_1d;                      break;
                case "texture_2dAttribute":                 e1 = Enum(args[0]); attr = texture_2d;                      break;
                case "texture_2d_arrayAttribute":           e1 = Enum(args[0]); attr = texture_2d_array;                break;
                case "texture_3dAttribute":                 e1 = Enum(args[0]); attr = texture_3d;                      break;
                case "texture_cubeAttribute":               e1 = Enum(args[0]); attr = texture_cube;                    break;
                case "texture_cube_arrayAttribute":         e1 = Enum(args[0]); attr = texture_cube_array;              break;
                //
                case "texture_multisampled_2dAttribute":    e1 = Enum(args[0]); attr = texture_multisampled_2d;         break;
                case "texture_depth_multisampled_2dAttribute":                  attr = texture_depth_multisampled_2d;   break;
                //
                case "texture_storage_1dAttribute":         e1 = Enum(args[0]); e2 = Enum(args[1]); attr = texture_storage_1d;      break;
                case "texture_storage_2dAttribute":         e1 = Enum(args[0]); e2 = Enum(args[1]); attr = texture_storage_2d;      break;
                case "texture_storage_2d_arrayAttribute":   e1 = Enum(args[0]); e2 = Enum(args[1]); attr = texture_storage_2d_array;break;
                case "texture_storage_3dAttribute":         e1 = Enum(args[0]); e2 = Enum(args[1]); attr = texture_storage_3d;      break;
                //
                case "texture_depth_2dAttribute":           attr = texture_depth_2d;            break;
                case "texture_depth_2d_arrayAttribute":     attr = texture_depth_2d_array;      break;
                case "texture_depth_cubeAttribute":         attr = texture_depth_cube;          break;
                case "texture_depth_cube_arrayAttribute":   attr = texture_depth_cube_array;    break;
                default:
                    continue;
            }
            attributeData = attribute;
        }
        return attr;
    }

    private static CsType MapType(SemanticInfo semanticInfo, ITypeSymbol typeSymbol, bool getFields)
    {
        bool isArray = false;
        if (typeSymbol is IArrayTypeSymbol arrayTypeSymbol) {
            isArray = true;
            typeSymbol = arrayTypeSymbol.ElementType;
        }
        var type = GetType(semanticInfo, typeSymbol, getFields);

        var genericTypes = new List<CsType>();

        if (typeSymbol is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            bool getFieldTypes = type.TypeCode.IsBuffer;
            foreach (var typeArg in namedType.TypeArguments)
            {
                var fieldType = GetType(semanticInfo, typeArg, getFieldTypes);
                genericTypes.Add(fieldType);
            }
        }
        return type with {
            Generics    = genericTypes.ToValueArray(),
            IsArray     = isArray
        };
    }

    private static string GetNamespace(ITypeSymbol? symbol)
    {
        return symbol?.ContainingNamespace?.IsGlobalNamespace == false
            ? symbol.ContainingNamespace.ToDisplayString()
            : string.Empty;
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
                return new CsTypeIdentifier (knownType);
            }
        }
        bool isPointerType = false;
        if (symbol is IPointerTypeSymbol pointerTypeSymbol) {
            symbol          = pointerTypeSymbol.PointedAtType;
            isPointerType   = true;
        }
        var ns = GetNamespace(symbol);
        
        var name = symbol?.Name ?? "UnknownType";
        if (isPointerType) {
            name += '*';
        }
        return new CsTypeIdentifier (name, ns);
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
