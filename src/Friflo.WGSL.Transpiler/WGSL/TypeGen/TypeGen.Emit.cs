// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Friflo.WGSL.Transpiler.CSharp;
using static Friflo.WGSL.Transpiler.WGSL.TypeResolution;

// ReSharper disable InlineTemporaryVariable
// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault
// ReSharper disable ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertIfStatementToConditionalTernaryExpression
// ReSharper disable RawStringCanBeSimplified
// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Friflo.WGSL.Transpiler.WGSL;


public sealed partial class TypeGen
{
    private readonly    Dictionary<string, CSharpStruct>    structMap                   = new();
    private readonly    Dictionary<string, LocalStruct>     localStructs                = new();
    private readonly    Dictionary<string, WgslStruct>      wgslStructs                 = new();
    private readonly    HashSet<string>                     requiredStructs             = [];
    private readonly    HashSet<string>                     emittedStructs              = [];
    //
    private readonly    StringBuilder                       fixedSizedArrayBuilder      = new();
    private readonly    Dictionary<string, FixedSizeArray>  globalFixedSizedArrayTypes  = new();
    private readonly    Dictionary<string, FixedSizeArray>  localFixedSizedArrayTypes   = new();

    private             WgslModule                          module;
    private             string                              fileNamespace;
    private             CSharpIdentifier[]                  TypeMap;

    private const string  LineFeeds = "\n\n\n";
        
    
    private void EmitStructs(StringBuilder sb, string normalizedPath)
    {
        CreateStructs();
        if (requiredStructs.Count == 0) {
            return;
        }
        foreach (var wgslStruct in module.Structs)
        {
            var structName = wgslStruct.Name;
            if (!localStructs.TryGetValue(structName, out var localStruct)) {
                continue;
            }
            if (!emittedStructs.Add(structName)) {
                sb.Append( // language=csharp
                    $"""
                    #error Duplicate identifier '{structName}'
                    [Source("~/{normalizedPath}")]
                    file partial class _info;
                    """).Append(LineFeeds);
                continue;
            }
            if (localStruct.alreadyDeclared) {
                sb.Append( // language=csharp
                    $"""
                    /// Skipped identical duplicate of  <see cref="{structName}"/>
                    file partial class _info;
                    """).Append(LineFeeds);
                continue;
            }
            var fields = localStruct.csharpStruct.fields;
            if (fields.Length == 0) {
                sb.Append( // language=csharp
                    $"""
                    #error Struct '{structName}' must contain at least one member. Empty structs are not allowed in WGSL.
                    [Source("~/{normalizedPath}")]
                    file partial class _info;
                    """).Append(LineFeeds);
                continue;
            }
            // FIX_C89_STRUCT_HACK
            // Ignore structs with: dynamic array<> field + other fields
            var arrayField = fields.FirstOrDefault(f => f.type.info.paramType == WgslParamType.DynamicArray);
            if (arrayField.name != null) {
                if (fields.Length > 1) {
                    var binding = module.Bindings.FirstOrDefault(b => b.WgslType.Name == structName);
                    EmitStructWithDynamicArrayField(sb, binding, localStruct.csharpStruct, arrayField, normalizedPath);
                }
                continue;
            }
            sb.Append( // language=csharp
                $"""
                [Source("~/{normalizedPath}")]
                [StructLayout(LayoutKind.Explicit, Size = {localStruct.csharpStruct.layout.size})]
                """);
            sb.Append(localStruct.csharpStruct.source);
        }
    }
    
    private void CreateStructs()
    {
        var structs  = module.Structs;
        if (module.Bindings.Count == 0 || structs.Count == 0) {
            return;
        }
        foreach (var wgslStruct in structs) {
            wgslStructs.TryAdd(wgslStruct.Name, wgslStruct);
        }
        foreach (var binding in module.Bindings)
        {
            var addressSpace    = binding.AddressSpace;
            var wgslType        = binding.WgslType;
            if (addressSpace == "storage" && wgslType.Name == "array" && wgslType.Generics.Length == 2) {
                // Skip: not useful to create a fixed size array for a storage buffer
                continue;
            }
            var alignment = addressSpace == "storage" ? ArrayStride.Natural : ArrayStride.PadTo16Bytes;
            GetCSharpType(wgslType, alignment); // calls CreateStruct() if referencing one
        }
    }
    
    private CSharpStruct CreateStruct(WgslStruct wgslStruct, ArrayStride arrayStride)
    {
        var structName  = wgslStruct.Name;
        if (localStructs.TryGetValue(structName, out var localStruct)) {
            return localStruct.csharpStruct;
        }
        var length      = wgslStruct.Fields.Count;
        var fields      = new CSharpField[length];
        var sb          = new StringBuilder();
        sb.Append($"\npublic struct {structName} (");
        
        var maxTypeWidth  = 0;
        var maxFieldWidth = 0;
        
        for (int n = 0; n < length; n++) {
            var field       = wgslStruct.Fields[n];
            var csharpType  = GetCSharpType(field.WgslType, arrayStride);
            fields[n]       = new CSharpField { name = field.Name, type = csharpType, wgslAlign = field.Align, wgslSize = field.Size };
            maxTypeWidth    = Math.Max(maxTypeWidth, csharpType.identifier.Name.Length);
            maxFieldWidth   = Math.Max(maxFieldWidth, field.Name.Length);
            AddNamespace(csharpType);
        }
        var layout = AssignFieldLayouts(fields, arrayStride);
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
            var identifier  = field.type.identifier;
            var padName     = maxTypeWidth  - identifier.Name.Length;
            var padAssign   = maxFieldWidth - field.name.Length;
            sb.Append($"    [FieldOffset({field.offset,3})]  public  {identifier.Name} ").Append(' ', padName);
            sb.Append($"{field.name} ").Append(' ', padAssign).Append($"= {field.name};");
            AppendTypeComment(sb, identifier, "  ", "");
            sb.Append("\n");
        }
        sb.Append("}").Append(LineFeeds);
        var source = sb.ToString();
        
        var fullQualifiedName   = $"{fileNamespace}-{structName}";
        
        if (structMap.TryGetValue(fullQualifiedName, out var curStruct)) {
            var alreadyDeclared = source == curStruct.source;
            if (alreadyDeclared) {
                localStructs.Add(curStruct.name, new LocalStruct { csharpStruct = curStruct, alreadyDeclared = true });
                return curStruct;
            }
        }
        var csharpStruct = new CSharpStruct { name = structName, source = source, fields = fields, layout = layout };
        structMap.TryAdd(fullQualifiedName, csharpStruct);
        localStructs.TryAdd(csharpStruct.name, new LocalStruct { csharpStruct = csharpStruct, alreadyDeclared = false });
        return csharpStruct;
    }
    
    private CSharpType GetCSharpType(WgslType type, ArrayStride arrayStride)
    {
        var info = WgslTypeInfo.GetTypeInfo(type);
        
        CSharpType csharpType;
        if (info.typeCode == CsTypeCode.None) {
            var typeName = info.IsArray ? info.elementType : type.ToString();
            if (wgslStructs.TryGetValue(typeName, out var wgslStruct)) {
                requiredStructs.Add(wgslStruct.Name);
                var csharpStruct = CreateStruct(wgslStruct, arrayStride);
                var structInfo = new WgslTypeInfo(CsTypeCode.WgslStruct, info.paramType, info.arraySize, info.elementType);
                csharpType = new CSharpType(typeName, Resolved, structInfo, csharpStruct);
            } else {
                // case: WGSL error 
                csharpType = new CSharpType(typeName, NotFound, info, null);
            }
        } else {
            var typeIdentifier = TypeMap[(int)info.typeCode];
            csharpType = new CSharpType(typeIdentifier, info, null);
        }
        if (info.paramType == WgslParamType.FixedSizeArray) {
            return EmitFixedSizeArray(csharpType, arrayStride);
        }
        return csharpType;
    }
    
    private static TypeLayout AssignFieldLayouts(CSharpField[] fields, ArrayStride arrayStride)
    {
        int currentOffset  = 0;
        int maxStructAlign = 1;
        for (int n = 0; n < fields.Length; n++)
        {
            var field   = fields[n];
            TypeLayout layout;
            var typeCode = field.type.info.typeCode;

            // retrieve base layout (struct oder wgsl type: i32, f32, vec3<f32>, mat4x4<f32>, ...)
            if (typeCode == CsTypeCode.WgslStruct) {
                var csharpStruct = field.type.csharpStruct;
                
                // Rebound nested struct layout with the same alignment mode
                if (csharpStruct.fields == null) {
                    layout = csharpStruct.layout; // element layout of struct in a fixed size array
                } else {
                    layout = AssignFieldLayouts(csharpStruct.fields, arrayStride);
                }
                
                // In std140 (Uniform), nested structs are rounded up to at least 16-byte alignment
                if (arrayStride == ArrayStride.PadTo16Bytes) {
                    int structAlign = Math.Max(layout.align, 16);
                    layout = new TypeLayout(layout.size, structAlign);
                }
            } else {
                layout = typeCode.Layout;
            }

            // adjust layout if FixedSizeArray
            if (field.type.info.paramType == WgslParamType.FixedSizeArray) {
                int elementSize   = layout.size;
                int elementAlign  = layout.align;
                int arrayCount    = field.type.info.arraySize;
                
                // Calculate natural stride
                int elementStride = (elementSize + (elementAlign - 1)) & ~(elementAlign - 1);
                int arrayAlign    = elementAlign;
                
                // In std140 (Uniform), both array element stride AND array alignment are at least 16 bytes
                if (arrayStride == ArrayStride.PadTo16Bytes) {
                    elementStride = Math.Max(elementStride, 16);
                    arrayAlign    = Math.Max(arrayAlign, 16);
                }
                int arraySize = elementStride * arrayCount;
                layout = new TypeLayout(arraySize, arrayAlign);
            }

            // apply WGSL @size and @align overrides
            int fieldSize  = field.wgslSize.HasValue  ? Math.Max(field.wgslSize.Value,  layout.size)  : layout.size;
            // @align must only increase layout.align
            int fieldAlign = field.wgslAlign.HasValue ? Math.Max(field.wgslAlign.Value, layout.align) : layout.align;
            
            layout = new TypeLayout(fieldSize, fieldAlign);

            // Track maximum alignment to determine total struct alignment
            maxStructAlign = Math.Max(maxStructAlign, layout.align);

            // Align current offset to field's required alignment boundary
            currentOffset    = (currentOffset + (layout.align - 1)) & ~(layout.align - 1);
            fields[n].offset = currentOffset;
            fields[n].size   = layout.size;

            currentOffset += layout.size;
        }

        // In std140 (Uniform), outer struct alignment is rounded up to at least 16 bytes
        if (arrayStride == ArrayStride.PadTo16Bytes) {
            maxStructAlign = Math.Max(maxStructAlign, 16);
        }

        // Struct size must be padded to a multiple of its alignment (struct stride)
        int finalStructSize = (currentOffset + (maxStructAlign - 1)) & ~(maxStructAlign - 1);
        return new TypeLayout(finalStructSize, maxStructAlign);
    }
    
    private static void AppendTypeComment(StringBuilder sb, CSharpIdentifier identifier, string head, string tail)
    {
        switch (identifier.resolution) {
            case Unmapped: sb.Append($"{head}// INFO: '{identifier.Name}' requires mapping in '{TypeMappings.MappingPath}'{tail}"); break;
            case NotFound: sb.Append($"{head}// WGSL error - missing type: '{identifier.Name}'{tail}");                             break;
        }
    }
    
    private CSharpType EmitFixedSizeArray(CSharpType type, ArrayStride arrayStride)
    {
        var arraySize   = type.info.arraySize;
        var typeCode    = type.info.typeCode;
        var identifier  = typeCode is CsTypeCode.WgslStruct or CsTypeCode.None
            ? type.identifier
            : TypeMap[(int)typeCode];
        
        var layout = typeCode == CsTypeCode.WgslStruct
            ? type.csharpStruct.layout
            : typeCode.Layout;
        
        var stride          = GetFixedSizeArrayStride(layout, arrayStride);
        var arrayName       = arrayStride == ArrayStride.PadTo16Bytes ? "_UniArr_" : "_Array_";
        var typeName        = $"{identifier.Name}{arrayName}{arraySize}";
        var qualifiedName   = $"{identifier.Namespace}-{typeName}";
        AddNamespace(type);
        
        var sb = fixedSizedArrayBuilder;
        sb.Clear();
        var fixedSizedArrays = typeCode == CsTypeCode.None || typeCode == CsTypeCode.WgslStruct ? localFixedSizedArrayTypes : globalFixedSizedArrayTypes;
        
        if (!fixedSizedArrays.ContainsKey(qualifiedName))
        {
            var sizeInBytes = stride * arraySize;
            var elementType = identifier.Name;
            AppendTypeComment(sb, identifier, "", "\n");
            sb.Append( // language=csharp
                $$"""
                [DebuggerTypeProxy(typeof(FixedArrayDebugView<{{elementType}}>))]
                [StructLayout(LayoutKind.Explicit, Size = {{sizeInBytes}})]
                public struct {{typeName}}
                {
                    public int  Length => {{arraySize}};
                    [FieldOffset(0)]  private {{elementType}} _element0;
                    
                    public ref {{elementType}} this[int index] {
                        [UnscopedRef] get {
                            if ((uint)index >= {{arraySize}}) throw new IndexOutOfRangeException();
                            return ref Unsafe.AddByteOffset(ref _element0, (nint)index * {{stride}});
                        }
                    }
                    
                    [UnscopedRef] public FixedArrayEnumerator<{{elementType}}> GetEnumerator() => new(ref _element0, {{stride}}, {{sizeInBytes}});
                }
                """).Append(LineFeeds);
            fixedSizedArrays.Add(qualifiedName, new FixedSizeArray { Name = typeName, Namespace = identifier.Namespace, source = sb.ToString() });
        } else {
            sb.Append( // language=csharp
                $$"""
                /// Skipped identical duplicate of  <see cref="{{typeName}}"/>
                file partial class _info;
                """).Append(LineFeeds);
        }
        var csharpArray = new CSharpStruct{ name = typeName, source = null, fields = null, layout = layout }; 
        return new CSharpType(typeName, Created, type.info, csharpArray);
    }
    
    private static int GetFixedSizeArrayStride(TypeLayout layout, ArrayStride arrayStride)
    {
        int elementSize  = layout.size;
        int elementAlign = layout.align;

        // ArrayStride.Natural (Storage):      stride is elementSize rounded up to elementAlign
        int strideNatural       = (elementSize + elementAlign - 1) & ~(elementAlign - 1);

        // ArrayStride.PadTo16Bytes (Uniform): element alignment is elevated to at least 16 bytes
        int requiredAlignPad16  = Math.Max(16, elementAlign);
        int stridePad16         = (elementSize + requiredAlignPad16 - 1) & ~(requiredAlignPad16 - 1);

        // Array requires PadTo16Bytes layout variant if stride differs from strideNatural
        // isPadTo16 = arrayStride == ArrayStride.PadTo16Bytes && stridePad16 != strideNatural;

        return arrayStride == ArrayStride.PadTo16Bytes ? stridePad16 : strideNatural;
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
""").Append(LineFeeds);
    }
}