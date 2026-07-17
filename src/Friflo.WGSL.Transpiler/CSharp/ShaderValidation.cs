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
    
    public ValidationError(SrcLoc srcLoc, string  message)
    {
        this.srcLoc     = srcLoc;
        this.message    = message;
    }
}

public static class ShaderValidation
{
    public static List<ValidationError> Validate(CsMethod method, ImmutableArray<WgslFile> files)
    {
        var errors = new List<ValidationError>();
        foreach (var shader in  method.Shaders)
        {
            var file = files.FirstOrDefault(file => file.NormalizedPath.EndsWith(shader.path));
            if (file.NormalizedPath == null) {
                errors.Add(new ValidationError(shader.pathLoc, $"'{shader.path}' not found"));
            }
        }
        return errors;
    }
}