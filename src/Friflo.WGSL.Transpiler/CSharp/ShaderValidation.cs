// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Friflo.WGSL.Transpiler.CodeFixes;

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
        var errors          = new List<ValidationError>();
        var wgslBindings    = new Dictionary<(int,int), WgslBinding>();
        
        foreach (var shader in  method.Shaders)
        {
            var file = files.FirstOrDefault(file => file.NormalizedPath.EndsWith(shader.path));
            if (file.NormalizedPath == null) {
                errors.Add(shader.pathLoc, $"'{shader.path}' not found");
                continue;
            }
            foreach (var error in file.Module.Errors) {
                errors.Add(shader.attrLoc, $"WGSL parser error: {error}");
            }
            foreach (var binding in file.Module.Bindings) {
                wgslBindings.TryAdd((binding.Group, binding.Binding), binding);
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
        var bindings = new Dictionary<(int,int), CsParameter>();
        foreach (var parameter in parameters)
        {
            if (!parameter.IsBindGroupEntry) continue;
            var bindGroup = parameter.BindGroup;
            if (bindGroup.group < 0 || bindGroup.group >= 4) {
                errors.Add(bindGroup.attrLoc, $"group must be in range: 0 - 3. was: {bindGroup.group}");
            }
            if (bindGroup.binding < 0 || bindGroup.binding >= 640) {
                errors.Add(bindGroup.attrLoc, $"binding must be in range: 0 - 639. was: {bindGroup.binding}");
            }
            if (!bindings.TryAdd((bindGroup.group, bindGroup.binding), parameter)) {
                errors.Add(bindGroup.attrLoc, $"binding already exists: [Map({bindGroup.group}, {bindGroup.binding})]");
            }
        }
        
        ValidateBindings(bindings, wgslBindings, errors);

        return errors;
    }
    
    private static void ValidateBindings(
        Dictionary<(int,int), CsParameter>  bindings,
        Dictionary<(int,int), WgslBinding>  wgslBindings,
        List<ValidationError>               errors)
    {
        foreach (var parameter in bindings.Values)
        {
            if (!parameter.IsBindGroupEntry) continue;
            var bindGroup = parameter.BindGroup;
            if (!wgslBindings.TryGetValue((bindGroup.group,  bindGroup.binding), out var wgslBinding)) {
                continue; 
            }
            var paramType = parameter.ParamAttribute.ToString();
            switch (parameter.ParamAttribute)
            {
                case CsParamAttribute.uniform:
                    if (!parameter.IsResource) {
                        
                        continue;
                    }
                    goto case CsParamAttribute.storage;
                case CsParamAttribute.storage:
                    if (wgslBinding.AddressSpace != paramType) {
                        // errors.Add(bindGroup.attrLoc, $"wgsl expect: <{wgslBinding.AddressSpace}>");
                    }
                    continue;
                case CsParamAttribute.sampler_NonFiltering:
                    paramType = "sampler";
                    goto case CsParamAttribute.sampler;
                //
                case CsParamAttribute.sampler:
                case CsParamAttribute.sampler_comparison:
                //
                case CsParamAttribute.texture_1d:
                case CsParamAttribute.texture_2d:
                case CsParamAttribute.texture_2d_array:
                case CsParamAttribute.texture_3d:
                case CsParamAttribute.texture_cube:
                case CsParamAttribute.texture_cube_array:
                //
                case CsParamAttribute.texture_multisampled_2d:
                case CsParamAttribute.texture_depth_multisampled_2d:
                //
                case CsParamAttribute.texture_storage_1d:
                case CsParamAttribute.texture_storage_2d:
                case CsParamAttribute.texture_storage_2d_array:
                case CsParamAttribute.texture_storage_3d:
                //
                case CsParamAttribute.texture_depth_2d:
                case CsParamAttribute.texture_depth_2d_array:
                case CsParamAttribute.texture_depth_cube:
                case CsParamAttribute.texture_depth_cube_array:
                    if (wgslBinding.WgslType.Name != paramType) {
                        errors.Add(bindGroup.attrLoc, $"C# [{paramType}]  wgsl expect: <{wgslBinding.WgslType.Name}>");
                    }
                    continue;
            }
        }
    }
}

