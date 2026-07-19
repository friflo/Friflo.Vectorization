// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Friflo.WGSL.Transpiler.CodeFixes;
using Friflo.WGSL.Transpiler.WGSL;
using static Friflo.WGSL.Transpiler.CSharp.CsParamAttribute;

// ReSharper disable InvertIf
// ReSharper disable InconsistentNaming
// ReSharper disable DuplicatedSwitchSectionBodies
// ReSharper disable RedundantJumpStatement
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable PossibleMultipleEnumeration
namespace Friflo.WGSL.Transpiler.CSharp;

public enum DiagType
{
    Error,
    Warn
}

public readonly struct ValidationDiag
{
    public readonly     SrcLoc      srcLoc;
    public readonly     string      message;
    public readonly     DiagType    type;

    public override string  ToString() => message;

    public ValidationDiag(SrcLoc srcLoc, string  message,  DiagType type)
    {
        this.srcLoc     = srcLoc;
        this.message    = message;
        this.type       = type;
    }
}

public static class ShaderValidation
{
    public static List<ValidationDiag> Validate(CsMethod method, ImmutableArray<WgslFile> files)
    {
        var diags           = new List<ValidationDiag>();
        var wgslBindings    = new Dictionary<(int,int), WgslBinding>();
        
        foreach (var shader in  method.Shaders)
        {
            var file = files.FirstOrDefault(file => file.NormalizedPath.EndsWith(shader.path));
            if (file.NormalizedPath == null) {
                diags.Shader(shader.pathLoc, shader, $"file not found", DiagType.Error);
                continue;
            }
            foreach (var error in file.Module.Errors) {
                diags.Shader(shader.attrLoc, shader, $"WGSL parser error - {error}", DiagType.Warn);
            }
            foreach (var binding in file.Module.Bindings) {
                wgslBindings.TryAdd((binding.Group, binding.Binding), binding);
            }
        }
        
        // parameters.Length == 0  must compile and execute to enable fast prototyping
        var parameters = method.Parameters;
        if (parameters.Length == 1) {
            diags.Method(method.MethodLoc, method, "missing required parameters  ->  require (RenderPass pass, RenderConfig config)", DiagType.Error);
        }
        else if (parameters.Length > 1) {
            if (parameters[0].Type.Name != "RenderPass") {
                diags.Method(parameters[0].TypeLoc, method, $"invalid first parameter type: {parameters[0].Type.Name}  ->  expected RenderPass", DiagType.Error);
            }
            if (parameters[1].Type.Name != "RenderConfig") {
                diags.Method(parameters[1].TypeLoc, method, $"invalid second parameter type: {parameters[1].Type.Name}  ->  expected RenderConfig", DiagType.Error);
            }
        }
        
        var indexBufferParameters = parameters.Where(p => p.ParamAttribute == IndexBuffer);
        if (indexBufferParameters.Count() > 1) {
            foreach (var parameter in indexBufferParameters) {
                diags.Map(parameter.AttrLoc, parameter, "Shader method must not have multiple [IndexBuffer] parameters", DiagType.Warn);    
            }
        }
        var bindings = new Dictionary<(int,int), CsParameter>();
        foreach (var parameter in parameters)
        {
            if (parameter.IsBindGroupEntry) {
                ValidateBinding(parameter, bindings, wgslBindings, diags);
            }
            ValidateParameter(parameter, diags);
        }
        
        if (parameters.Length > 0) {
            // no errors on shader methods without parameters for fast prototyping
            foreach (var wgslBinding in wgslBindings.Values) {
                if (!bindings.ContainsKey((wgslBinding.Group, wgslBinding.Binding))) {
                    var msg = $"missing C# parameter [Map({wgslBinding.Group}, {wgslBinding.Binding})] {wgslBinding.Name}  ->  {wgslBinding.AsString()}";
                    diags.Method(method.MethodLoc, method, msg, DiagType.Warn);
                }
            }
        }
        return diags;
    }
    
    extension(List<ValidationDiag> diags)
    {
        private void Shader(SrcLoc srcLoc, CsShader shader, string message, DiagType type) {
            var error = $"[Shader(\"{shader.path}\")] - {message}";
            diags.Add(new ValidationDiag(srcLoc, error, type));
        }
                
        private void Method(SrcLoc srcLoc, CsMethod method, string message, DiagType type) {
            var error = $"{method.Name} - {message}";
            diags.Add(new ValidationDiag(srcLoc, error, type));
        }

        private void Map(SrcLoc srcLoc, CsParameter parameter, string message, DiagType type) {
            var bg = parameter.BindGroup;
            var error = $"[Map({bg.group}, {bg.binding})] {parameter.Name} - {message}";
            diags.Add(new ValidationDiag(srcLoc, error, type));
        }
        
        private void TypeMismatch(CsParameter parameter, WgslBinding wgslBinding)
        {
            var sb = new StringBuilder();
            sb.Append("type mismatch: C# [");
            sb.Append(parameter.ParamAttribute);
            var start = sb.Length;
            var arg_0 = parameter.AttrEnum.enum1.Name;
            if (!string.IsNullOrEmpty(arg_0)) {
                sb.Append("(");
                sb.Append(arg_0);
            }
            var arg_1 = parameter.AttrEnum.enum2.Name;
            if (!string.IsNullOrEmpty(arg_1)) {
                sb.Append(", ");
                sb.Append(arg_1);
            }
            if (sb.Length > start) {
                sb.Append(")");
            }
            sb.Append("]  ->  ");
            sb.Append(wgslBinding.AsString());
            diags.Map(parameter.AttrLoc, parameter, sb.ToString(), DiagType.Warn);
        }
        
        private void ParameterType(CsParameter parameter, string expectedCSharpType)
        {
            var error = $"[{parameter.ParamAttribute}] requires Type: {expectedCSharpType}";
            diags.Add(new ValidationDiag(parameter.TypeLoc, error, DiagType.Error));
        }
    }
    
    private static void ValidateBinding(
        CsParameter                         parameter,
        Dictionary<(int,int), CsParameter>  bindings,
        Dictionary<(int,int), WgslBinding>  wgslBindings,
        List<ValidationDiag>                diags)
    {
        var bindGroup = parameter.BindGroup;
        if (bindGroup.group < 0 || bindGroup.group >= 4) {
            diags.Map(bindGroup.attrLoc, parameter, $"group must be in range: 0 - 3. was: {bindGroup.group}", DiagType.Error);
        }
        else if (bindGroup.binding < 0 || bindGroup.binding >= 640) {
            diags.Map(bindGroup.attrLoc, parameter, $"binding must be in range: 0 - 639. was: {bindGroup.binding}", DiagType.Warn);
        }
        else if (!bindings.TryAdd((bindGroup.group, bindGroup.binding), parameter)) {
            diags.Map(bindGroup.attrLoc, parameter, "binding already exists", DiagType.Error);
        }
        else {
            if (!wgslBindings.TryGetValue((bindGroup.group,  bindGroup.binding), out var wgslBinding)) {
                diags.Map(parameter.BindGroup.attrLoc, parameter, "binding not declared in wgsl", DiagType.Warn);
            } else {
                ValidateBindingType(parameter, wgslBinding, diags);
            }
        }
    }

    
    private static void ValidateBindingType(CsParameter parameter, WgslBinding wgslBinding, List<ValidationDiag> diags)
    {
        var paramType = parameter.ParamAttribute.ToString();
        switch (parameter.ParamAttribute)
        {
            case uniform:
            case storage:
                if (paramType != wgslBinding.AddressSpace) {
                    diags.TypeMismatch(parameter, wgslBinding);
                }
                return;
            
            // --- Sampler types
            case sampler_NonFiltering:
                paramType = "sampler";  // maps to sampler. no sampler_NonFiltering in WGSL
                goto case sampler;
            case sampler:
            case sampler_comparison:
                if (paramType != wgslBinding.WgslType?.Name) {
                    diags.TypeMismatch(parameter, wgslBinding);
                }
                return;
                
            // --- Texture Types
            case texture_1d:
            case texture_2d:
            case texture_2d_array:
            case texture_3d:
            case texture_cube:
            case texture_cube_array:
            //
            case texture_multisampled_2d:
                if (paramType != wgslBinding.WgslType?.Name ||
                    parameter.AttrEnum.enum1.Name != wgslBinding.GetGenericNameAt(0))
                {
                    diags.TypeMismatch(parameter, wgslBinding);
                }
                return;
            //
            case texture_storage_1d:
            case texture_storage_2d:
            case texture_storage_2d_array:
            case texture_storage_3d:
                var format = WgslTextureFormat.MapWgslStorageFormatToEnumName(wgslBinding.GetGenericNameAt(0));
                if (paramType != wgslBinding.WgslType?.Name ||
                    parameter.AttrEnum.enum1.Name != format ||
                    parameter.AttrEnum.enum2.Name != wgslBinding.GetGenericNameAt(1))
                {
                    diags.TypeMismatch(parameter, wgslBinding);
                }
                return;
            //
            case texture_depth_multisampled_2d:
            //
            case texture_depth_2d:
            case texture_depth_2d_array:
            case texture_depth_cube:
            case texture_depth_cube_array:
                if (paramType != wgslBinding.WgslType?.Name) {
                    diags.TypeMismatch(parameter, wgslBinding);
                }
                return;
        }
    }
    
    private static void ValidateParameter(CsParameter parameter, List<ValidationDiag> diags)
    {
        var typeName = parameter.Type.Name;
        switch (parameter.ParamAttribute)
        {
            case uniform:
                return;
            case storage:
            case VertexBuffer:
                if (typeName != "InBuffer" && typeName != "InOutBuffer") {
                    diags.ParameterType(parameter, "InBuffer<> or InOutBuffer<>");
                }
                return;
            case IndexBuffer:
                if (typeName != "InBuffer" && typeName != "InOutBuffer") {
                    diags.ParameterType(parameter, "InBuffer<> or InOutBuffer<>");
                } else {
                    var generics = parameter.Type.Generics; 
                    var genericType = generics.Length == 1 ? generics[0].Name : "";
                    if (genericType != "ushort" && genericType != "uint") {
                        diags.ParameterType(parameter, "with <ushort> or <uint>");
                    }
                }
                return;
            
            // --- Sampler types
            case sampler_NonFiltering:
            case sampler:
            case sampler_comparison:
                if (typeName != "GpuSampler") {
                    diags.ParameterType(parameter, "GpuSampler");
                }
                return;
                
            // --- Texture Types
            case texture_1d:
            case texture_2d:
            case texture_2d_array:
            case texture_3d:
            case texture_cube:
            case texture_cube_array:
            //
            case texture_multisampled_2d:
            case texture_depth_multisampled_2d:
            //
            case texture_storage_1d:
            case texture_storage_2d:
            case texture_storage_2d_array:
            case texture_storage_3d:
            //
            case texture_depth_2d:
            case texture_depth_2d_array:
            case texture_depth_cube:
            case texture_depth_cube_array:
                if (typeName != "GpuTextureView") {
                    diags.ParameterType(parameter, "GpuTextureView");
                }
                return;
        }
    }
}

