// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Friflo.WGSL.Transpiler.CSharp;

namespace Friflo.WGSL.Transpiler.WGSL;


public readonly struct WgslType2CSharpType
{
    public readonly CsTypeCode          typeCode;
    public readonly CsTypeIdentifier    identifier;
}


public readonly struct CSharpType
{
    public readonly CsTypeIdentifier    identifier;
    public readonly WgslTypeInfo        info;
    public readonly CSharpStruct        csharpStruct; // != null if struct
    
    public          CsTypeIdentifier    ElementType => info.typeCode == CsTypeCode.None ? new CsTypeIdentifier(info.elementType) : TypeMap[(int)info.typeCode];
        
    public override string              ToString()  => info.ToString();
    
    private static readonly  CsTypeIdentifier[] TypeMap;


    internal CSharpType(string typeName, WgslTypeInfo info, CSharpStruct csharpStruct) {
        this.identifier     = new CsTypeIdentifier(typeName);
        this.info           = info;
        this.csharpStruct   = csharpStruct;
    }
        
    internal CSharpType(CsTypeIdentifier identifier, WgslTypeInfo info, CSharpStruct csharpStruct) {
        this.identifier     = identifier;
        this.info           = info;
        this.csharpStruct   = csharpStruct;
    }
    
    private static void MapType(CsTypeIdentifier[] typeCodeMap, CsTypeCode code, string typeName) {
        typeCodeMap[(int)code] = new CsTypeIdentifier(typeName);
    }
    
    static CSharpType()
    {
        const int length = (int)CsTypeCode.WgslStruct;
        var tcMap   = TypeMap = new CsTypeIdentifier[length];
        var values  = Enum.GetValues(typeof(CsTypeCode)).Cast<CsTypeCode>();
        
        foreach (var value in values) {
            if ((int)value >= length) continue;
            MapType(tcMap, value, value.ToString());
        }
        MapType(tcMap, CsTypeCode.f16,     "Half");
        MapType(tcMap, CsTypeCode.f32,     "float");
        MapType(tcMap, CsTypeCode.i32,     "int");
        MapType(tcMap, CsTypeCode.u32,     "uint");
        
        MapType(tcMap, CsTypeCode.vec2f,   "Vector2");
        MapType(tcMap, CsTypeCode.vec3f,   "Vector3");
        MapType(tcMap, CsTypeCode.vec4f,   "Vector4");
        
        MapType(tcMap, CsTypeCode.mat4x4f, "Matrix4x4");
        MapType(tcMap, CsTypeCode.mat3x2f, "Matrix3x2");
    }
    
    internal static CSharpType GetCSharpType(WgslType wgslType, WgslTypeInfo info)
    {
        if (info.typeCode == CsTypeCode.None) {
            return new CSharpType(wgslType.ToString(), info, null);    
        }
        var identifier = TypeMap[(int)info.typeCode];
        return new CSharpType(identifier, info, null);
    }
}