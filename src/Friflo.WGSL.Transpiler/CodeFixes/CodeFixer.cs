// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Friflo.WGSL.Transpiler.CSharp;
using Friflo.WGSL.Transpiler.WGSL;

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
    private static (string vsEntry, string fsEntry) GetEntryPoints(CsMethod method, ImmutableArray<WgslFile> files)
    {
        foreach (var shader in method.Shaders) {
            
        }
        return default;
    }
    
    public static WgslModule ParseWgslFiles(List<WgslFile> files)
    {
        var fullModule = new WgslModule();
        foreach (var file in files) 
        {
            var module = WgslParser.ParseWgsl(file.Content, file.NormalizedPath);
            fullModule.AddModule(module);
        }
        return fullModule;
    }
    
    public static List<WgslFile> FilterFiles(CsMethod method, ImmutableArray<WgslFile> files)
    {
        var result = new List<WgslFile>();
        foreach (var shader in method.Shaders) 
        {
            var file = files.FirstOrDefault(f => f.NormalizedPath.EndsWith(shader.path));
            if (file.NormalizedPath == null) continue;
            result.Add(file with { NormalizedPath = shader.path });
        }
        return result;
    }
    
    public static ShaderParamsResult CreateShaderParams(WgslModule module)
    {
        var sb      = new StringBuilder();
        sb.Append("(RenderPass pass, RenderConfig config,");
        
        var parameters  = new List<MethodParam>();
        
        // --- [Map(group, binding)] [...]
        var bindings = CreateBindings(module);
        AddBindingParameters(module, parameters, bindings);
        
        // --- [VertexBuffer(0)]
        AddVertexBufferParameters(parameters, module.EntryPoints);
        
        AppendParameters(sb, parameters);
        
        var comments = CreateComments(parameters);
        
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
    
    private static void AddBindingParameters(WgslModule module, List<MethodParam> parameters, List<WgslBinding> bindings)
    {
        foreach (var binding in bindings)
        {
            switch (binding.AddressSpace)
            {
            case "storage":
                var type = TypeGenerator.GetBindingType(module, binding);
                if (type == null) {
                    continue;
                }
                TypeGenerator.TryGetKnownCSharpType(type, out var csType);
                var bufferType = binding.AccessMode == "read" ? "InBuffer" : "InOutBuffer";
                parameters.Add(new MethodParam(binding, "[storage]", $"{bufferType}<{csType}>"));
                break;
            case "uniform":
                type = TypeGenerator.GetBindingType(module, binding);
                if (type == null) {
                    continue;
                }
                TypeGenerator.TryGetKnownCSharpType(type, out csType);
                parameters.Add(new MethodParam(binding, "[uniform]", $"in {csType}"));
                break;
            case "":
                AppendWgslType(parameters, binding);
                break;
            }
        }
    }
    
    private static void AppendWgslType(List<MethodParam> parameters, WgslBinding binding)
    {
        var wgslType    = binding.WgslType;
        var name        = wgslType.Name;
        var generics    = wgslType.Generics;
        var arg0        = generics.Length > 0 ? generics[0].Name : null;
        var arg1        = generics.Length > 1 ? generics[1].Name : null;

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
        parameters.Add(new MethodParam("[VertexBuffer(0)]", "InBuffer<float>", parameterName, $"[ ]  Adjust the generic type of '{parameterName}' to your vertex struct."));
    }
    
    private static string CreateComments(List<MethodParam> parameters)
    {
        var sb = new StringBuilder();
        sb.Append("    // [ ]  Add [Draw] to the vertex buffer parameter used to execute the draw call.\n");
        
        foreach (var param in parameters) {
            if (param.comment == null)  continue;
            sb.Append($"    // {param.comment}\n");
        }
        sb.Append("    // [ ]  If needed, add parameter: [IndexBuffer] InBuffer<ushort|uint> indices.\n"); // This cannot be inferred from wgsl.
        return sb.ToString();
    }
}