// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Text;

// ReSharper disable SuggestVarOrType_SimpleTypes
namespace Friflo.WGSL.Transpiler;

public static class CodeFixer
{
    public static string CreateShaderParams(string wgsl)
    {
        WgslShaderMetadata shader = WgslSuperpowerParser.ParseShader(wgsl);
        var sb = new StringBuilder();
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