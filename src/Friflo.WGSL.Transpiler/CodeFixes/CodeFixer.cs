// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Friflo.WGSL.Transpiler.CSharp;
using Friflo.WGSL.Transpiler.WGSL;

// ReSharper disable UseCollectionExpression
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertIfStatementToReturnStatement
// ReSharper disable InvertIf
// ReSharper disable SuggestVarOrType_SimpleTypes
namespace Friflo.WGSL.Transpiler.CodeFixes;



public readonly struct ShaderParamsResult
{
    public required string  Parameters  { get; init; }
    public required string  Comments    { get; init; }
}


public static partial class CodeFixer
{
    public static WgslModule ParseWgslFiles(WgslFile[] files)
    {
        var fullModule = new WgslModule();
        foreach (var file in files) 
        {
            var module = FastWgslParser.ParseWgsl(file.Content, file.NormalizedPath);
            fullModule.AddModule(module);
        }
        return fullModule;
    }
    
    public static ImmutableArray<WgslFile> FilterFiles(CsMethod method, ImmutableArray<WgslFile> files, out string workload)
    {
        workload = "draw";
        var result = new List<WgslFile>();
        foreach (var shader in method.Shaders) 
        {
            var file = files.FirstOrDefault(f => f.NormalizedPath.EndsWith(shader.path));
            if (file.NormalizedPath == null) continue;
            if (shader.compute != null) workload = "compute";
            result.Add(file with { NormalizedPath = shader.path });
        }
        return result.ToImmutableArray();
    }
    
    public static ShaderParamsResult CreateShaderParams(WgslModule module, TypeMapping[] mappings, bool isCompute)
    {
        var typeMap = TypeMapping.CreateTypeMap(mappings);
        var sb      = new StringBuilder();
        
        if (isCompute) {
            sb.Append("(PipelineContext computeContext,");
        } else {
            sb.Append("(RenderPass pass, RenderConfig config,");
        }
        
        var parameters  = new List<MethodParam>();
        
        // --- [Map(group, binding)] [...]
        var bindings = CreateBindings(module);
        AddBindingParameters(module, parameters, bindings, typeMap);
        
        // --- [VertexBuffer(0)]
        AddVertexBufferParameters(parameters, module.EntryPoints);
        
        AppendParameters(sb, parameters);
        
        var comments = CreateComments(parameters, module, isCompute);
        
        return new ShaderParamsResult {
            Parameters  = sb.ToString(),
            Comments    = comments,  
        };
    }

    private static List<WgslBinding> CreateBindings(WgslModule module)
    {
        var bindings    = new List<WgslBinding>();
        var bindingMap  = new Dictionary<(int, int), WgslBinding>();

        // --- remove duplicate binding
        foreach (var binding in module.Bindings)
        {
            var key = (binding.Group, binding.Binding);
            if (!bindingMap.ContainsKey(key)) {
                bindingMap.Add(key, binding);
                bindings.Add(binding);
            }
        }
        bindings.Sort((a, b) => {
            int groupComparison = a.Group.CompareTo(b.Group);
            if (groupComparison != 0) {
                return groupComparison;
            }
            return a.Binding.CompareTo(b.Binding);
        });
        return bindings;
    }
    
    private static void AddBindingParameters(WgslModule module, List<MethodParam> parameters, List<WgslBinding> bindings, CSharpIdentifier[] typeMap)
    {
        foreach (var binding in bindings)
        {
            switch (binding.AddressSpace)
            {
            case "storage": {
                var type        = GetStorageBindingType(module, binding);
                var csType      = GetParameterType(type, typeMap, out _);
                var bufferType  = binding.AccessMode == "write" ? "InOutBuffer" : "InBuffer";
                csType          = $"{bufferType}<{csType}>";
                parameters.Add(new MethodParam(binding, "[storage]", csType));
                break;
            }
            case "uniform": {
                var type        = binding.WgslType;
                string? comment  = null;
                var csType      = GetParameterType(type, typeMap, out var info);
                switch (info.paramType) {
                    case WgslParamType.DynamicArray: 
                        comment = $"#warning A uniform must not use dynamic sized buffers. See:  {binding}";
                        csType = $"in {csType}";
                        break;
                    case WgslParamType.FixedSizeArray:
                        csType = $"in {csType}_UniArr_{info.arraySize}";
                        break;
                    case WgslParamType.None:
                        csType = $"in {csType}";
                        break;
                }
                parameters.Add(new MethodParam(binding, "[uniform]", csType, comment));
                break;
            }
            case "":
                AppendWgslType(parameters, binding);
                break;
            }
        }
    }
    
    private static WgslType GetStorageBindingType(WgslModule module, WgslBinding binding)
    {
        var type = module.Structs.FirstOrDefault(s => s.Name == binding.WgslType.Name);
        // FIX_C89_STRUCT_HACK
        // In case a struct contains exactly one field return the field type 
        if (type != null && type.Fields.Count == 1) {
            var fieldType = type.Fields[0].WgslType;
            var info = WgslTypeInfo.GetTypeInfo(fieldType);
            var paramType = info.paramType;
            if (paramType == WgslParamType.DynamicArray) {
                return fieldType.Generics.Arg_0;
            }
        }
        return binding.WgslType;
    }
    
    private static string GetParameterType(WgslType type, CSharpIdentifier[] typeMap, out WgslTypeInfo info)
    {
        info = WgslTypeInfo.GetTypeInfo(type);
        if (info.typeCode == CsTypeCode.None) {
            return info.IsArray ? info.elementType! : type.ToString();
        }
        return typeMap[(int)info.typeCode].Name;
    }
    
    private static void AppendWgslType(List<MethodParam> parameters, WgslBinding binding)
    {
        var wgslType    = binding.WgslType;
        var name        = wgslType.Name;
        var generics    = wgslType.Generics;
        var length      = generics.Length;
        var arg0        = length > 0 ? generics.Arg_0.Name : null;
        var arg1        = length > 1 ? generics.Arg_1.Name : null;

        switch (name)
        {
            // --- WGSL Sampler Types           See:  https://www.w3.org/TR/WGSL/#sampler-type
            case "sampler":
                parameters.Add(new MethodParam(binding, "[sampler]",              "GpuSampler"));
                break;
            case "sampler_comparison":
                parameters.Add(new MethodParam(binding, "[sampler_comparison]",   "GpuSampler"));
                break;
            
            // ------ WGSL texture types
            
            // --- Sampled Texture Types        See:  https://www.w3.org/TR/WGSL/#sampled-texture-type
            case "texture_1d":
            case "texture_2d":
            case "texture_2d_array":
            case "texture_3d":
            case "texture_cube":
            case "texture_cube_array":
                var sampleType = arg0 ?? "f32";
                parameters.Add(new MethodParam(binding, $"[{name}(ST.{sampleType})]",     "GpuTextureView"));
                break;
            
            // --- Multisampled Texture Types   See:  https://www.w3.org/TR/WGSL/#multisampled-texture-type
            case "texture_multisampled_2d":
                sampleType = arg0 ?? "f32";
                parameters.Add(new MethodParam(binding, $"[{name}(ST.{sampleType})]",     "GpuTextureView"));
                break;
            case "texture_depth_multisampled_2d":
                parameters.Add(new MethodParam(binding, $"[{name}]",                      "GpuTextureView"));
                break;
            
            // --- Storage Texture Types        See:  https://www.w3.org/TR/WGSL/#texture-storage
            case "texture_storage_1d":
            case "texture_storage_2d":
            case "texture_storage_2d_array":
            case "texture_storage_3d":
                var format = WgslTextureFormat.MapWgslStorageFormatToEnumName(arg0);
                var access = arg1 ?? "read";
                parameters.Add(new MethodParam(binding, $"[{name}(TextureFormat.{format}, TSA.{access})]", "GpuTextureView"));
                break;
            
            // --- Depth Texture Types          See:  https://www.w3.org/TR/WGSL/#texture-depth
            case "texture_depth_2d":
            case "texture_depth_2d_array":
            case "texture_depth_cube":
            case "texture_depth_cube_array":
                parameters.Add(new MethodParam(binding, $"[{name}]",  "GpuTextureView"));
                break;
        }
    }

    private static void AddVertexBufferParameters(List<MethodParam> parameters, List<WgslEntryPoint> entryPoints)
    {
        var hasVertexLocations = entryPoints
            .Any(ep => ep.Stage == "vertex" && ep.Parameters.Any(p => p.Attribute.StartsWith("@location")));

        if (!hasVertexLocations) {
            return;
        }
        var parameterName = "vertexBuffer";
        if (parameters.Any(p => p.name == parameterName)) {
            parameterName = "vertices";
            if (parameters.Any(p => p.name == parameterName)) {
                parameterName = "vertexInputBuffer";
            }
        }
        parameters.Add(new MethodParam("[VertexBuffer(0)]", "InBuffer<float>", parameterName, $"// [ ]  Adjust the generic type of '{parameterName}' to your vertex struct."));
    }
    
    private static string CreateComments(List<MethodParam> parameters, WgslModule module, bool isCompute)
    {
        var sb = new StringBuilder();
        if (isCompute) {
            var computeEntry = module.EntryPoints.Find(ep => ep.Stage == "compute");
            var workgroupSize = computeEntry?.Attributes.FirstOrDefault(attr => attr.Name == "workgroup_size");
            var args = workgroupSize == null ? "64, 1, 1" : string.Join(", ", workgroupSize.Args);
            sb.Append($"    // [ ]  Add [Dispatch({args})] to the storage buffer parameter used to execute DispatchWorkgroups().\n");
        } else {
            sb.Append("    // [ ]  Add [Draw] to the vertex buffer parameter used to execute the draw call.\n");
        }
        
        foreach (var param in parameters) {
            if (param.comment == null)  continue;
            sb.Append($"    {param.comment}\n");
        }
        if (!isCompute) {
            sb.Append("    // [ ]  If needed, add parameter: [IndexBuffer] InBuffer<ushort|uint> indices.\n"); // This cannot be inferred from wgsl.
        }
        return sb.ToString();
    }
}
