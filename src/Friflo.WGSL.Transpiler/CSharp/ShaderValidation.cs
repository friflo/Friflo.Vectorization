// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
// ReSharper disable ConvertToPrimaryConstructor

namespace Friflo.WGSL.Transpiler.CSharp;

public readonly struct ValidationError
{
    public readonly SrcLoc  srcLoc;
    public readonly string  message;

    public override string  ToString() => message;

    public ValidationError(SrcLoc srcLoc, string  message)
    {
        this.srcLoc     = srcLoc;
        this.message    = message;
    }
}

public static class ShaderValidation
{
    private static void Add(this List<ValidationError> errors, SrcLoc srcLoc, string message) {
        errors.Add(new ValidationError(srcLoc, message));
    }
    
    public static List<ValidationError> Validate(CsMethod method, ImmutableArray<WgslFile> files)
    {
        var errors = new List<ValidationError>();
        foreach (var shader in  method.Shaders)
        {
            var file = files.FirstOrDefault(file => file.NormalizedPath.EndsWith(shader.path));
            if (file.NormalizedPath == null) {
                errors.Add(shader.pathLoc, $"'{shader.path}' not found");
            }
        }
        var parameters = method.Parameters;
        if (parameters.Length > 0)
        {
            var param = parameters[0]; 
            if (param.Type.Name != "RenderPass") {
                errors.Add(param.TypeLoc, "expect first parameter Type: RenderPass");
            }
        }
        if (parameters.Length > 1)
        {
            var param = parameters[1]; 
            if (param.Type.Name != "RenderConfig") {
                errors.Add(param.TypeLoc, "expect second parameter Type: RenderConfig");
            }
        }
        return errors;
    }
}