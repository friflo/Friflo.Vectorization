// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Collections.Immutable;
using System.Text;
using Friflo.WGSL.Transpiler.CSharp;

// ReSharper disable SuggestVarOrType_SimpleTypes
namespace Friflo.WGSL.Transpiler;

public static class CodeFixer
{
    public static string CreateShaderParams(CsMethod method, ImmutableArray<WgslFile> files)
    {
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
        
        sb.Append("(RenderPass pass, RenderConfig config,\n");

        foreach (var binding in shaderMeta.Bindings) {
            switch (binding.AddressSpace)
            {
            case "storage":
                var bufferType = binding.AccessMode == "read" ? "InBuffer" : "InOutBuffer";
                sb.Append($"        [BindStorage({binding.Group}, {binding.Binding})]         {bufferType}<{binding.WgslType}> {binding.Name},\n");
                break;
            case "uniform":
                sb.Append($"        [BindUniform({binding.Group}, {binding.Binding})]         in {binding.WgslType} {binding.Name},\n");
                break;
            case "":
                AppendWgslType(sb, binding);
                break;
            }
        }
        sb.Length -= 2;
        sb.Append(")");
        return sb.ToString();
    }
    
    private static void AppendWgslType(StringBuilder sb, WgslBinding binding)
    {
        var wgslType = binding.WgslType;
        switch (wgslType)
        {
            case "sampler":
                sb.Append($"        [SamplerFilteringAttribute({binding.Group}, {binding.Binding})]    GpuSampler {binding.Name},\n");
                break;
            case "sampler_comparison":
                sb.Append($"        [SamplerComparison({binding.Group}, {binding.Binding})]    GpuSampler {binding.Name},\n");
                break;
            case "texture_depth_2d":
                sb.Append($"        [{wgslType}({binding.Group}, {binding.Binding})]    GpuTextureView {binding.Name},\n");
                break;
            
        }
        
    }
}