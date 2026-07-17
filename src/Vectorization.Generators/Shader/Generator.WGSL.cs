// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Threading;
using Friflo.WGSL.Transpiler.CodeFixes;
using Friflo.WGSL.Transpiler.CSharp;
using Microsoft.CodeAnalysis;


// ReSharper disable once CheckNamespace
namespace Friflo;


public sealed partial class ShaderGen
{
    private static WgslFile CreateWgslFile(AdditionalText text, CancellationToken cancellationToken)
    {
        var content = text.GetText(cancellationToken)?.ToString() ?? string.Empty;
        var path    = text.Path.Replace('\\', '/');
        var module  = WgslParser.ParseShader(content);
        return new WgslFile {
            NormalizedPath  = path,
            Hash            = ComputeFnv1A64(content),
            Content         = content,
            Module          = module
        };
    }
}
