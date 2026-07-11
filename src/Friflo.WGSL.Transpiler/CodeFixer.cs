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

        foreach (var b in shaderMeta.Bindings) {
            switch (b.AddressSpace)
            {
            case "storage":
                var bufferType = b.AccessMode == "read" ? "InBuffer" : "InOutBuffer";
                sb.Append($"        [BindStorage({b.Group}, {b.Binding})] {bufferType}<{b.WgslType}> {b.Name},\n");
                break;
            case "uniform":
                sb.Append($"        [BindUniform({b.Group}, {b.Binding})] in {b.WgslType} {b.Name},\n");
                break;
            }
        }
        sb.Length -= 2;
        sb.Append(")");
        return sb.ToString();
    }
}