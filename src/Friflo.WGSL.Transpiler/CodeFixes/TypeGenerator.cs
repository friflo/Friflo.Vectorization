// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Linq;
using Friflo.WGSL.Transpiler.CSharp;
using Friflo.WGSL.Transpiler.WGSL;

// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable SuggestVarOrType_BuiltInTypes
namespace Friflo.WGSL.Transpiler.CodeFixes;


public static class TypeGenerator
{
    internal static WgslType GetBindingType(WgslModule module, WgslBinding binding, out bool isArray)
    {
        isArray = false;
        switch (binding.AddressSpace)
        {
            case "uniform":
            case "storage":
                var type = module.Structs.FirstOrDefault(s => s.Name == binding.WgslType.Name);
                // FIX_C89_STRUCT_HACK
                // In case a struct contains exactly one field return the field type 
                if (type != null && type.Fields.Count == 1) {
                    var fieldType = type.Fields[0].WgslType;
                    if (fieldType.Name == "array" && fieldType.Generics.Length == 1) {
                        isArray = true;
                        return fieldType.Generics.Arg_0;
                    }
                }
                return binding.WgslType;
        }
        return null;
    }
    
    
    internal static bool TryGetKnownCSharpType(WgslType type, CSharpIdentifier[] typeMap, ref bool isArray, out string csType)
    {
        var info = WgslTypeInfo.GetTypeInfo(type);
        if (info.typeCode == CsTypeCode.None) {
            if (info.IsArray) {
                isArray = true;    
            }
            csType  = info.IsArray ? info.elementType : type.ToString();
            return false;
        }
        csType = typeMap[(int)info.typeCode].Name;
        return true;
    }
}