// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable PossibleMultipleEnumeration
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
        
        // parameters.Length == 0  must compile and execute to enable fast prototyping
        var parameters = method.Parameters;
        if (parameters.Length == 1) {
            errors.Add(method.MethodLoc, "expect two parameters: RenderPass pass, RenderConfig config");
        }
        else if (parameters.Length > 1) {
            if (parameters[0].Type.Name != "RenderPass") {
                errors.Add(parameters[0].TypeLoc, "expect first parameter Type: RenderPass");
            }
            if (parameters[1].Type.Name != "RenderConfig") {
                errors.Add(parameters[1].TypeLoc, "expect second parameter Type: RenderConfig");
            }
        }
        
        var vertexParameters = parameters.Where(p => p.ParamAttribute == CsParamAttribute.IndexBuffer);
        if (vertexParameters.Count() > 1) {
            foreach (var parameter in vertexParameters) {
                errors.Add(parameter.AttrLoc, "Shader method must not have multiple [IndexBuffer] parameters");    
            }
        }
        var bindings = new HashSet<(int,int)>();
        foreach (var parameter in parameters) {
            if (!parameter.IsBindGroupEntry) continue;
            var bindGroup = parameter.BindGroup;
            if (bindGroup.group < 0 || bindGroup.group >= 4) {
                errors.Add(bindGroup.attrLoc, $"group must be in range: 0 - 3. was: {bindGroup.group}");
            }
            if (bindGroup.binding < 0 || bindGroup.binding >= 640) {
                errors.Add(bindGroup.attrLoc, $"binding must be in range: 0 - 639. was: {bindGroup.binding}");
            }
            if (!bindings.Add((bindGroup.group, bindGroup.binding))) {
                errors.Add(bindGroup.attrLoc, $"binding already exists: [Map({bindGroup.group}, {bindGroup.binding})]");
            }
        }
        return errors;
    }
}