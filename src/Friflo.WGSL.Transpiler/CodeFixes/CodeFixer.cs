// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Friflo.WGSL.Transpiler.CSharp;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertIfStatementToReturnStatement
// ReSharper disable InvertIf
// ReSharper disable SuggestVarOrType_SimpleTypes
namespace Friflo.WGSL.Transpiler.CodeFixes;



public readonly struct WgslValidationError
{
    public required string                  Message  { get; init; }
}


public readonly struct CodeFixerResult
{
    public required string                  Parameters  { get; init; }
    public required WgslValidationError[]   Errors      { get; init; }
}


public static partial class CodeFixer
{
    private static (string vsEntry, string fsEntry) GetEntryPoints(CsMethod method, ImmutableArray<WgslFile> files)
    {
        foreach (var shader in method.Shaders) {
            
        }
        return default;
    }
    
    public static string CreateWgsl(CsMethod method, ImmutableArray<WgslFile> files)
    {
        var sb      = new StringBuilder();
        // var (vsEntry, fsEntry) = GetEntryPoints(method, files);
        foreach (var file in files)
        {
            foreach (var shader in method.Shaders) {
                if (!file.NormalizedPath.EndsWith(shader.path)) continue;
                sb.Append(file.Content);
                break;
            }
        }
        return sb.ToString();
    }
    
    public static CodeFixerResult CreateShaderParams(WgslModule module)
    {
        var sb      = new StringBuilder();
        sb.Append("(RenderPass pass, RenderConfig config,");
        
        var errors      = new List<WgslValidationError>();
        var parameters  = new List<MethodParam>();
        
        // --- [Map(group, binding)] [...]
        var bindings = CreateBindings(module, errors);
        AddBindingParameters(module, parameters, bindings);
        
        // --- [VertexBuffer(0)]
        AddVertexBufferParameters(parameters, module.EntryPoints);
        
        AppendParameters(sb, parameters);
        
        return new CodeFixerResult {
            Parameters  = sb.ToString(),
            Errors      = errors.ToArray() // new WgslValidationError { Message = "XXX Test some WGSL message" }]
        };
    }

    private static List<WgslBinding> CreateBindings(WgslModule module, List<WgslValidationError> errors)
    {
        var bindings    = new List<WgslBinding>();
        var bindingMap  = new Dictionary<(int, int), WgslBinding>();

        // --- remove duplicate binding
        foreach (var binding in module.Bindings)
        {
            var key = (binding.Group, binding.Binding);
            if (bindingMap.TryGetValue(key, out var value)) {
                if (!value.Equals(binding)) { 
                    errors.Add(new WgslValidationError { Message = $"inconsistent binding for: @group({binding.Group}) @binding({binding.Binding})" });
                }
            } else {
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

        foreach (var binding in bindings) {
            switch (binding.AddressSpace)
            {
            case "storage":
                var type = TypeGenerator.GetBindingType(module, binding);
                if (type == null) {
                    continue;
                } 
                var bufferType = binding.AccessMode == "read" ? "InBuffer" : "InOutBuffer";
                parameters.Add(new MethodParam(binding, "[storage]", $"{bufferType}<{type}>"));
                break;
            case "uniform":
                parameters.Add(new MethodParam(binding, "[uniform]", $"in {binding.WgslType}"));
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
                var format = arg0 ?? "read";
                var access = arg1 ?? "RGBA8Unorm";
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
        foreach (var entryPoint in entryPoints)
        {
            if (entryPoint.Stage == "vertex")
            {
                var foundVertexBuffers = 0;
                foreach (var parameter in entryPoint.Parameters)
                {
                    if (parameter.Attribute.StartsWith("@location")) {
                        if (foundVertexBuffers == 0) {
                            parameters.Add(new MethodParam("[VertexBuffer(0)]", "InBuffer<float>", parameter.Name, "Opt: [IndexBuffer] InBuffer<ushort|uint> indices"));
                        }
                        foundVertexBuffers++;
                    }
                }
            }
        } 
    }
}