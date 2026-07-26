// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Friflo.WGSL.Transpiler.CSharp;

// ReSharper disable RawStringCanBeSimplified
// ReSharper disable ConvertIfStatementToConditionalTernaryExpression
// ReSharper disable ConvertIfStatementToReturnStatement
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming
namespace Friflo.WGSL.Transpiler.WGSL;


public sealed class TypeEmitter
{
    private readonly    Dictionary<string, CSharpStruct>    structMap               = new();
    private readonly    Dictionary<string, LocalStruct>     localStructs            = new();
    private readonly    Dictionary<string, WgslStruct>      wgslStructs             = new();
    private readonly    HashSet<string>                     requiredStructs         = [];
    //
    private readonly    StringBuilder                       fixedSizedArrays        = new();
    private readonly    HashSet<string>                     fixedSizedArrayTypes    = [];
    private readonly    HashSet<string>                     additionalNamespaces    = [];

    private             WgslModule                          module;
    private             string                              fileNamespace;
    private             CsTypeIdentifier[]                  TypeMap;
    
    private static void DebugInputs(WgslFile[] wgslFiles, string projDir)
    {
        var path = Path.Combine(projDir, "debug.txt");
        var sb = new StringBuilder();
        sb.Append($"projDir: {projDir}\n\n");
        
        foreach (var file in wgslFiles) {
            sb.Append($"{file.NormalizedPath}\n");
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }
    
    private static string PathToNamespace(string path, string root = "")
    {
        var dir = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(dir)) return root;

        var parts = dir.Split(['/', '\\', '-', '_'], StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            var p = parts[i];
            var rest = p.Length > 1 ? p.Substring(1) : "";
            parts[i] = (char.IsDigit(p[0]) ? "_" : "") + char.ToUpperInvariant(p[0]) + rest;
        }
        return $"{root}{string.Join(".", parts)}";
    }
    
    private CsTypeIdentifier GetIdentifier(in CSharpType type)
    {
        return type.info.typeCode == CsTypeCode.None
            ? new CsTypeIdentifier(type.info.elementType, fileNamespace)
            : TypeMap[(int)type.info.typeCode];
    }
        
    private static void MapType(CsTypeIdentifier[] typeCodeMap, CsTypeCode code, string typeName) {
        typeCodeMap[(int)code] = new CsTypeIdentifier(typeName);
    }
    
    private static CsTypeIdentifier[] CreateTypeMap(WgslType2CSharpType[] wgslType2CSharpType)
    {
        const int length = (int)CsTypeCode.WgslStruct;
        var map     = new CsTypeIdentifier[length];
        var values  = Enum.GetValues(typeof(CsTypeCode)).Cast<CsTypeCode>();
        
        foreach (var value in values) {
            if ((int)value >= length) continue;
            MapType(map, value, value.ToString());
        }
        MapType(map, CsTypeCode.f16,     "Half");
        MapType(map, CsTypeCode.f32,     "float");
        MapType(map, CsTypeCode.i32,     "int");
        MapType(map, CsTypeCode.u32,     "uint");
        
        MapType(map, CsTypeCode.vec2f,   "Vector2");
        MapType(map, CsTypeCode.vec3f,   "Vector3");
        MapType(map, CsTypeCode.vec4f,   "Vector4");
        
        MapType(map, CsTypeCode.mat4x4f, "Matrix4x4");
        MapType(map, CsTypeCode.mat3x2f, "Matrix3x2");

        foreach (var mapping in wgslType2CSharpType) {
            map[(int)mapping.typeCode] = mapping.identifier;
        }
        return map;
    }
    
    private void AddNamespace(in CSharpType csharpType)
    {
        if (csharpType.identifier.Namespace == "") {
            return;
        }
        additionalNamespaces.Add(csharpType.identifier.Namespace);
    }
    
    public void EmitAllStructs(WgslFile[] wgslFiles, string projDir, WgslType2CSharpType[] mappings, string error)
    {
        var errorFilePath = $"{projDir}/generator-error.cs";
        if (error == null) {
            if (File.Exists(errorFilePath)) {
                File.Delete(errorFilePath);    
            }
        } else {
            File.WriteAllText(errorFilePath, $"#error {error}", new UTF8Encoding(false));
        }
        TypeMap = CreateTypeMap(mappings);
        
        for (int n = 0; n < wgslFiles.Length; n++) {
            var path =  wgslFiles[n].NormalizedPath.Substring(projDir.Length + 1);
            wgslFiles[n] = wgslFiles[n] with{ NormalizedPath =  path };
        }
        // DebugInputs(wgslFiles, projDir);
        
        // sort for deterministic generation
        WgslFile.Sort(wgslFiles);
        
        var source = new StringBuilder();
        var body   = new StringBuilder();
        
        foreach (var file in wgslFiles)
        {
            var normalizedPath = file.NormalizedPath;
            try {
                // --- clear state first!
                source.Clear();
                body.Clear();
                localStructs.Clear();
                requiredStructs.Clear();
                wgslStructs.Clear();
                fixedSizedArrays.Clear();
                additionalNamespaces.Clear();
                fileNamespace = PathToNamespace(normalizedPath);
                
                // --- process after
                module = WgslParser.ParseWgsl(file.Content, normalizedPath);
                EmitStructs(body, normalizedPath);
                if (body.Length == 0) {
                    continue;
                }
                source.Append( // language=csharp
                    $"""
                    // <auto-generated />
                    using System;
                    using System.Numerics;
                    using System.Runtime.CompilerServices;
                    using System.Runtime.InteropServices;
                    using Friflo.Vectorization.WebGPU;
                    
                    """);
                foreach (var ns in additionalNamespaces) {
                    source.Append($"using {ns};\n");
                }
                source.Append( // language=csharp
                    $"""
                    
                    namespace {fileNamespace};
                    
                    
                    {body}{fixedSizedArrays}
                    """);
            }
            catch (Exception exception) {
                source.Append( // language=csharp
                    $"""
                    /* -------- Error parsing: {normalizedPath}
                    {WgslUtils.GetExceptionAsString(exception)}
                    */
                    """);
            }
            var content =  source.ToString();
            var path    = $"{projDir}/{file.NormalizedPath}.cs";

            File.WriteAllText(path, content, new UTF8Encoding(false));
        }
    }
    
    private void CreateStructs()
    {
        var structs  = module.Structs;
        if (module.Bindings.Count == 0 || structs.Count == 0) {
            return;
        }
        foreach (var wgslStruct in structs) {
            if (!wgslStructs.TryAdd(wgslStruct.Name, wgslStruct)) {
                // TODO  emit warning
            }
        }
        foreach (var binding in module.Bindings) {
            var typeName = binding.WgslType.Name;
            if (wgslStructs.ContainsKey(typeName)) {
                requiredStructs.Add(typeName);
            }
        }
        foreach (var wgslStruct in structs) {
            if (requiredStructs.Contains(wgslStruct.Name)) {
                CreateStruct(wgslStruct);
            }
        }
    }
        
    private void EmitStructs(StringBuilder sb, string normalizedPath)
    {
        CreateStructs();
        if (requiredStructs.Count == 0) {
            return;
        }
        foreach (var wgslStruct in module.Structs)
        {
            if (!localStructs.TryGetValue(wgslStruct.Name, out var localStruct)) {
                continue;
            }
            if (localStruct.alreadyDeclared) {
                sb.Append($"/// Skipped identical duplicate of  <see cref=\"{wgslStruct.Name}\"/>\nfile partial class _info;\n\n\n");
                continue;
            }
            var fields = localStruct.csharpStruct.fields;
            if (fields.Length == 0) {
                sb.Append( // language=csharp
                    $"""
                    #error Struct '{localStruct.csharpStruct.name}' must contain at least one member. Empty structs are not allowed in WGSL.
                    [Source("~/{normalizedPath}")]
                    file partial class _info;
                    """);
                sb.Append("\n\n\n");
                continue;
            }
            ////
            // FIX_C89_STRUCT_HACK
            // Ignore structs with dynamic array<> fields
            var arrayField = fields.FirstOrDefault(f => f.type.info.paramType == WgslParamType.DynamicArray);
            if (arrayField.name != null) {
                if (fields.Length > 1) {
                    var binding = module.Bindings.FirstOrDefault(b => b.WgslType.Name == wgslStruct.Name);
                    EmitStructWithDynamicArrayField(sb, binding, localStruct.csharpStruct, arrayField, normalizedPath);
                }
                continue;
            }
            sb.Append($"[Source(\"~/{normalizedPath}\")]\n");
            sb.Append($"[StructLayout(LayoutKind.Explicit, Size = {localStruct.csharpStruct.layout.size})]\n");
            sb.Append(localStruct.csharpStruct.source);
        }
    }
    
    private CSharpStruct CreateStruct(WgslStruct wgslStruct)
    {
        var structName  = wgslStruct.Name;
        if (localStructs.TryGetValue(structName, out var localStruct)) {
            return localStruct.csharpStruct;
        }
        var length      = wgslStruct.Fields.Count;
        var fields      = new CSharpField[length];
        var sb          = new StringBuilder();
        sb.Clear();
        sb.Append($"public struct {structName} (");
        
        var maxTypeWidth  = 0;
        var maxFieldWidth = 0;
        
        for (int n = 0; n < length; n++) {
            var field       = wgslStruct.Fields[n];
            var csharpType  = GetCSharpType(field.WgslType);
            fields[n]       = new CSharpField { name = field.Name, type = csharpType };
            maxTypeWidth    = Math.Max(maxTypeWidth, csharpType.identifier.Name.Length);
            maxFieldWidth   = Math.Max(maxFieldWidth, field.Name.Length);
            AddNamespace(csharpType);
        }
        var layout = AssignFieldLayouts(fields);
        foreach (var csharpField in fields) {
            var modifier = csharpField.size <= 16 ? "" : "in ";
            sb.Append($"{modifier}{csharpField.type.identifier.Name} {csharpField.name}, ");
        }
        if (length > 0) {
            sb.Length -= 2;
        }
        sb.Append(")\n");
        sb.Append("{\n");

        foreach (var field in fields) {
            var padName   = maxTypeWidth  - field.type.identifier.Name.Length;
            var padAssign = maxFieldWidth - field.name.Length;
            sb.Append($"    [FieldOffset({field.offset,3})]  public  {field.type.identifier.Name} ").Append(' ', padName);
            sb.Append($"{field.name} ").Append(' ', padAssign).Append($"= {field.name};\n");
        }
        sb.Append("}\n\n\n");
        var source = sb.ToString();
        
        var fullQualifiedName   = $"{fileNamespace}-{structName}";
        
        if (structMap.TryGetValue(fullQualifiedName, out var curStruct)) {
            var alreadyDeclared = source == curStruct.source;
            localStructs.Add(curStruct.name, new LocalStruct { csharpStruct = curStruct, alreadyDeclared = alreadyDeclared });
            return curStruct;
        }
        var csharpStruct = new CSharpStruct { name = structName, source =  source, fields = fields, layout = layout };
        structMap.Add(fullQualifiedName, csharpStruct);
        localStructs.Add(csharpStruct.name, new LocalStruct { csharpStruct = csharpStruct, alreadyDeclared = false });
        return csharpStruct;
    }
    
    private CSharpType GetCSharpType(WgslType type)
    {
        var args = GenericArgs.Create(type.Generics);
        
        var info = WgslTypeInfo.GetTypeInfo(type.Name, args);
        
        CSharpType csharpType;
        if (info.typeCode == CsTypeCode.None) {
            csharpType = new CSharpType(type.ToString(), info, null);    
        } else {
            var typeIdentifier = TypeMap[(int)info.typeCode];
            csharpType = new CSharpType(typeIdentifier, info, null);
        }
        
        if (info.paramType == WgslParamType.FixedSizeArray) {
            return CreateFixedSizeArray(csharpType);
        }
        if (info.typeCode != CsTypeCode.None) {
            return csharpType;
        }
        var identifier = info.IsArray ? new CsTypeIdentifier(info.elementType) : csharpType.identifier;
        if (!wgslStructs.TryGetValue(identifier.Name, out var wgslStruct)) {
            return csharpType;
        }
        requiredStructs.Add(wgslStruct.Name);
        var csharpStruct = CreateStruct(wgslStruct);
        var structInfo   = new WgslTypeInfo(CsTypeCode.WgslStruct, info.paramType, info.arraySize, info.elementType);
        return new CSharpType(identifier, structInfo, csharpStruct);
    }
    
    private static TypeLayout AssignFieldLayouts(CSharpField[] fields)
    {
	    int currentOffset  = 0;
	    int maxStructAlign = 1;
        for (int n = 0; n < fields.Length; n++)
        {
            var field   = fields[n];
            TypeLayout layout;
            var typeCode = field.type.info.typeCode;
            if (typeCode == CsTypeCode.WgslStruct) {
                var csharpStruct = field.type.csharpStruct;
                layout = csharpStruct != null ? AssignFieldLayouts(csharpStruct.fields) : default;
            } else {
                layout = typeCode.Layout;
                if (field.type.info.paramType == WgslParamType.FixedSizeArray) {
                    int elementSize     = layout.size;
                    int elementAlign    = layout.align;
                    int arrayCount      = field.type.info.arraySize;
                    int elementStride   = (elementSize + (elementAlign - 1)) & ~(elementAlign - 1);
                    int arraySize       = elementStride * arrayCount;
                    layout = new TypeLayout(arraySize, elementAlign);
                }
            }
            // TODO implement later
            // if (field.type.HasAlignAttribute) align = field.type.AlignAttributeValue;
		    // if (field.type.HasSizeAttribute)  size =  Math.Max(size, field.type.SizeAttributeValue);
            maxStructAlign = Math.Max(maxStructAlign, layout.align);
		    
            currentOffset       = (currentOffset + (layout.align - 1)) & ~(layout.align - 1);
            fields[n].offset    = currentOffset;
            fields[n].size      = layout.size;
            currentOffset += layout.size;
        }
        // Struct fields (nested structs) align to their maximum internal alignment (maxStructAlign).
        // Their size (finalStructSize) pads up to a multiple of that alignment (struct stride).
        int finalStructSize = (currentOffset + (maxStructAlign - 1)) & ~(maxStructAlign - 1);
        return new TypeLayout(finalStructSize, maxStructAlign);
    }
    
    private CSharpType CreateFixedSizeArray(CSharpType type)
    {
        var arraySize   = type.info.arraySize;
        var identifier  = GetIdentifier(type);
        var typeName    = $"{identifier.Name}_Array_{arraySize}";
        AddNamespace(type);
        
        if (fixedSizedArrayTypes.Add(typeName)) {
            fixedSizedArrays.Append( // language=csharp
                $$"""
                [InlineArray({{arraySize}})]
                public struct {{typeName}}
                {
                    private {{identifier.Name}} _element0;
                }
                """);
        } else {
            fixedSizedArrays.Append( // language=csharp
                $$"""
                /// Skipped identical duplicate of  <see cref="{{typeName}}"/>
                file partial class _info;
                """);
        }
        fixedSizedArrays.Append("\n\n\n");
        return new CSharpType(typeName, type.info, null);
    }
    
    private static void EmitStructWithDynamicArrayField(
        StringBuilder   sb,
        WgslBinding     binding,
        CSharpStruct    csharpStruct,
        CSharpField     arrayField,
        string          path)
    {
        var bindingName = binding.Name;
        var structName  = csharpStruct.name;
        var fieldName   = arrayField.name;
        var elementType = arrayField.type.info.ToString();
        
        sb.Append( // language=csharp
$"""
#error Unsupported Struct Layout in '{path}'
[Source("~/{path}")]
/*
Struct '{structName}' contains header fields alongside a dynamic array ('{elementType}').
Combining header data and dynamic arrays in a single Storage Buffer is intentionally restricted.

REASON:
- The legacy C89/C90 Struct Hack introduces complex byte-alignment and implicit padding issues between C# and WebGPU.
- Merging varying header data with static arrays degrades GPU cache performance.
- It makes usage of buffer header structs very complex and error prone.

RECOMMENDED FIX: Keep your struct as a clean Uniform Header with minimal changes:

1. Keep 'struct {structName}', but remove the dynamic array from it. Remove:
     {fieldName}: {elementType}

2. Use '{structName}' directly as a <uniform> binding for your header data:
     @group({binding.Group}) @binding({binding.Binding}) var<uniform> {bindingName}Uniform : {structName};

3. Declare the dynamic array as its own standalone <storage> binding:
     @group({binding.Group}) @binding({binding.Binding + 1}) var<storage, read> {bindingName} : {elementType};

4. In your shader functions, replace '{bindingName}.{fieldName}[i]' with '{bindingName}[i]'.
*/
file partial class _info;
""");
            
        
    }
}