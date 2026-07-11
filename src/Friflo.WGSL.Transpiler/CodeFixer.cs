// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Collections.Generic;
using System.Text;

// ReSharper disable SuggestVarOrType_SimpleTypes
namespace Friflo.WGSL.Transpiler;

public static class CodeFixer
{
    public static string CreateShaderParams(List<string> wgslContents)
    {
        var sb = new StringBuilder();
        foreach (var wgslContent in wgslContents) {
            sb.Append(wgslContent);
        }
        var wgsl = sb.ToString();
        sb.Clear();

        WgslShaderMetadata shader = WgslSuperpowerParser.ParseShader(wgsl);
        
        sb.Append("(RenderPass pass, RenderConfig config,\n");

        foreach (var b in shader.Bindings) {
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