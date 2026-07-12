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
    public required WgslModule              Module      { get; init; }
}


public static class CodeFixer
{
    private static (string vsEntry, string fsEntry) GetEntryPoints(CsMethod method, ImmutableArray<WgslFile> files)
    {
        foreach (var shader in method.Shaders) {
            
        }
        return default;
    }
    
    private static string CreateWgsl(StringBuilder sb, CsMethod method, ImmutableArray<WgslFile> files)
    {
        // var (vsEntry, fsEntry) = GetEntryPoints(method, files);
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
        return wgsl;
    }
    
    public static CodeFixerResult CreateShaderParams(CsMethod method, ImmutableArray<WgslFile> files)
    {
        var sb      = new StringBuilder();
        var wgsl    = CreateWgsl(sb, method, files);
        
        sb.Append("(RenderPass pass, RenderConfig config,\n");

        var module = WgslSuperpowerParser.ParseShader(wgsl);
        
        var errors = new List<WgslValidationError>();
        
        // --- [Map(group, binding)] [...]
        var bindings = CreateBindings(module, errors);
        AddBindingParameters(sb, bindings);
        
        // --- [VertexBuffer(0)]
        AddVertexBufferParameters(sb, module.EntryPoints);
        
        sb.Length -= 2;
        sb.Append(")");
        
        return new CodeFixerResult {
            Parameters  = sb.ToString(),
            Errors      = errors.ToArray(), // new WgslValidationError { Message = "XXX Test some WGSL message" }]
            Module      = module
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
    
    private static void AddBindingParameters(StringBuilder sb, List<WgslBinding> bindings)
    {
        var bindGroups  = new List<BindGroup>();
        foreach (var binding in bindings) {
            switch (binding.AddressSpace)
            {
            case "storage":
                var bufferType = binding.AccessMode == "read" ? "InBuffer" : "InOutBuffer";
                bindGroups.Add(new BindGroup(binding, "[storage]", $"{bufferType}<{binding.WgslType}>"));
                break;
            case "uniform":
                bindGroups.Add(new BindGroup(binding, "[uniform]", $"in {binding.WgslType}"));
                break;
            case "":
                AppendWgslType(bindGroups, binding);
                break;
            }
        }
        foreach (var bindGroup in bindGroups) {
            sb.Append($"        [Map({bindGroup.group}, {bindGroup.binding})] {bindGroup.attribute}         {bindGroup.type} {bindGroup.parameter},\n");
        }
    }
    
    private readonly struct BindGroup
    {
        public readonly int     group;
        public readonly int     binding;
        public readonly string  attribute;
        public readonly string  type;
        public readonly string  parameter;

        public override string ToString() => parameter;

        internal BindGroup(WgslBinding binding, string attribute, string type)
        {
            group           = binding.Group;
            this.binding    = binding.Binding;
            this.attribute  = attribute;
            this.type       = type;
            parameter       = binding.Name;
        }
    };
    
    private static void AppendWgslType(List<BindGroup> bindGroups, WgslBinding binding)
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
                bindGroups.Add(new BindGroup(binding, "[sampler]",              "GpuSampler"));
                break;
            case "sampler_comparison":
                bindGroups.Add(new BindGroup(binding, "[sampler_comparison]",   "GpuSampler"));
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
                bindGroups.Add(new BindGroup(binding, $"[{name}(ST.{sampleType})]",     "GpuTextureView"));
                break;
            
            // --- Multisampled Texture Types   See:  https://www.w3.org/TR/WGSL/#multisampled-texture-type
            case "texture_multisampled_2d":
                sampleType = arg0 ?? "f32";
                bindGroups.Add(new BindGroup(binding, $"[{name}(ST.{sampleType})]",     "GpuTextureView"));
                break;
            case "texture_depth_multisampled_2d":
                bindGroups.Add(new BindGroup(binding, $"[{name}]",                      "GpuTextureView"));
                break;
            
            // --- Storage Texture Types        See:  https://www.w3.org/TR/WGSL/#texture-storage
            case "texture_storage_1d":
            case "texture_storage_2d":
            case "texture_storage_2d_array":
            case "texture_storage_3d":
                var format = arg0 ?? "read";
                var access = arg1 ?? "RGBA8Unorm";
                bindGroups.Add(new BindGroup(binding, $"[{name}(TextureFormat.{format}, TSA.{access})]", "GpuTextureView"));
                break;
            
            // --- Depth Texture Types          See:  https://www.w3.org/TR/WGSL/#texture-depth
            case "texture_depth_2d":
            case "texture_depth_2d_array":
            case "texture_depth_cube":
            case "texture_depth_cube_array":
                bindGroups.Add(new BindGroup(binding, $"[{name}]",  "GpuTextureView"));
                break;
        }
    }

    private static void AddVertexBufferParameters(StringBuilder sb, List<WgslEntryPoint> entryPoints)
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
    }
}