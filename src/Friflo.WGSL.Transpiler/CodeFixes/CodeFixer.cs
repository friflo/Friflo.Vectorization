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
    public required WgslShaderMetadata      Metadata    { get; init; }
}


public static class CodeFixer
{
    private static (string vsEntry, string fsEntry) GetEntryPoints(CsMethod method, ImmutableArray<WgslFile> files)
    {
        foreach (var shader in method.Shaders) {
            
        }
        return default;
    }
    
    public static CodeFixerResult CreateShaderParams(CsMethod method, ImmutableArray<WgslFile> files)
    {
        // var (vsEntry, fsEntry) = GetEntryPoints(method, files);
        var sb = new StringBuilder();
        foreach (var file in files)
        {
            foreach (var shader in method.Shaders) {
                if (!file.NormalizedPath.EndsWith(shader.path)) continue;
                sb.Append(file.Content);
                break;
            }
        }
        var wgsl = sb.ToString();
        sb.Clear();

        WgslShaderMetadata shaderMeta = WgslSuperpowerParser.ParseShader(wgsl);
        
        var errors      = new List<WgslValidationError>();
        var bindings    = new List<WgslBinding>();
        var bindingMap  = new Dictionary<(int, int), WgslBinding>();

        // --- remove duplicate binding
        foreach (var binding in shaderMeta.Bindings)
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
        SortBindings(bindings);
        
        sb.Append("(RenderPass pass, RenderConfig config,\n");
        
        foreach (var entryPoint in shaderMeta.EntryPoints)
        {
            if (entryPoint.Stage == "vertex")
            {
                var foundVertexBuffers = 0;
                foreach (var parameter in entryPoint.Parameters)
                {
                    if (parameter.Attribute.StartsWith("@location")) {
                        if (foundVertexBuffers == 0) {
                            sb.Append($"        [VertexBuffer({0})]           InBuffer<float> {parameter.Name}, // Opt: [IndexBuffer] InBuffer<ushort|uint> indices,");
                        } else {
                            // sb.Append($"  |  {parameter.Name} {parameter.Attribute}");
                        }
                        foundVertexBuffers++;
                    }
                }
                if (foundVertexBuffers > 0) sb.Append("\n");
            }
        }
        
        foreach (var binding in bindings) {
            switch (binding.AddressSpace)
            {
            case "storage":
                var bufferType = binding.AccessMode == "read" ? "InBuffer" : "InOutBuffer";
                sb.Append($"        [Bind({binding.Group}, {binding.Binding})] [Storage]         {bufferType}<{binding.WgslType}> {binding.Name},\n");
                break;
            case "uniform":
                sb.Append($"        [Bind({binding.Group}, {binding.Binding})] [Uniform]         in {binding.WgslType} {binding.Name},\n");
                break;
            case "":
                AppendWgslType(sb, binding);
                break;
            }
        }
        sb.Length -= 2;
        sb.Append(")");
        
        return new CodeFixerResult {
            Parameters  = sb.ToString(),
            Errors      = errors.ToArray(), // new WgslValidationError { Message = "XXX Test some WGSL message" }]
            Metadata    = shaderMeta
        };
    }
    
    private static void AppendWgslType(StringBuilder sb, WgslBinding binding)
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
                sb.Append($"        [Bind({binding.Group}, {binding.Binding})] [SamplerFiltering]    GpuSampler {binding.Name},\n");
                break;
            case "sampler_comparison":
                sb.Append($"        [Bind({binding.Group}, {binding.Binding})] [SamplerComparison]    GpuSampler {binding.Name},\n");
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
                sb.Append($"        [Bind({binding.Group}, {binding.Binding})] [{name}(ST.{sampleType})]    GpuTextureView {binding.Name},\n");
                break;
            
            // --- Multisampled Texture Types   See:  https://www.w3.org/TR/WGSL/#multisampled-texture-type
            case "texture_multisampled_2d":
                sampleType = arg0 ?? "f32";
                sb.Append($"        [Bind({binding.Group}, {binding.Binding})] [{name}(ST.{sampleType})]    GpuTextureView {binding.Name},\n");
                break;
            case "texture_depth_multisampled_2d":
                sb.Append($"        [Bind({binding.Group}, {binding.Binding})] [{name}]    GpuTextureView {binding.Name},\n");
                break;
            
            // --- Storage Texture Types        See:  https://www.w3.org/TR/WGSL/#texture-storage
            case "texture_storage_1d":
            case "texture_storage_2d":
            case "texture_storage_2d_array":
            case "texture_storage_3d":
                var format = arg0 ?? "read";
                var access = arg1 ?? "RGBA8Unorm";
                sb.Append($"        [Bind({binding.Group}, {binding.Binding})] [{name}(TextureFormat.{format}, TSA.{access})]    GpuTextureView {binding.Name},\n");
                break;
            
            // --- Depth Texture Types          See:  https://www.w3.org/TR/WGSL/#texture-depth
            case "texture_depth_2d":
            case "texture_depth_2d_array":
            case "texture_depth_cube":
            case "texture_depth_cube_array":
                sb.Append($"        [Bind({binding.Group}, {binding.Binding})] [{name}]    GpuTextureView {binding.Name},\n");
                break;
        }
    }
    
    private static void SortBindings(List<WgslBinding> bindings)
    {
        bindings.Sort((a, b) => {
            int groupComparison = a.Group.CompareTo(b.Group);
            if (groupComparison != 0) {
                return groupComparison;
            }
            return a.Binding.CompareTo(b.Binding);
        });
    }
}