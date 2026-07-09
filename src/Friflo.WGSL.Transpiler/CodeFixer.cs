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
        sb.Append("(RenderPass pass, RenderConfig config, ");

        foreach (var binding in shader.Bindings) {
            var b = binding;
        }
        sb.Length -= 2;
        sb.Append(")");
        return sb.ToString();
    }
}