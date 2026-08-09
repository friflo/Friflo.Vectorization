// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Friflo.WGSL.Transpiler.CodeFixes;
using Friflo.WGSL.Transpiler.WGSL;
using static Friflo.WGSL.Transpiler.CSharp.CsParamAttribute;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable MergeIntoPattern
// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault
// ReSharper disable MergeIntoLogicalPattern
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
        var computeModule   = default(WgslModule?);
        var computeEntry    = default(string?);
        
        var bindingTypes    = new Dictionary<(int, int), CSharpType>();
        var typeMap         = TypeMapping.CreateTypeMap([]);
        var typeBuilder     = new TypeBuilder(typeMap, bindingTypes);
        
        foreach (var shader in  method.Shaders)
        {
            var file = files.FirstOrDefault(file => file.NormalizedPath.EndsWith(shader.path));
            if (file.NormalizedPath == null) {
                diags.Shader(shader.pathLoc, shader, $"file not found", DiagType.Error);
                continue;
            }
            var module = file.Module;
            if (shader.compute != null) {
                computeEntry    = shader.compute;
                computeModule   = module;
            }
            if (module == null) {
                continue;
            }
            foreach (var error in module.Errors) {
                diags.Shader(shader.attrLoc, shader, $"WGSL parser error - {error}", DiagType.Warn);
            }
            ValidateShader(shader, module, diags);
            typeBuilder.AddModuleType(module, shader.path);
            
            foreach (var binding in module.Bindings) {
                wgslBindings.TryAdd((binding.Group, binding.Binding), binding);
            }
        }
        ValidateWorkgroupSize(method, computeModule, computeEntry, diags);
        
        // parameters.Length == 0  must compile and execute to enable fast prototyping
        var parameters = method.Parameters;
        var isCompute = method.WorkgroupSize != null;
        if (isCompute) {
            if (parameters.Length > 0 && parameters[0].Type.Name != "PipelineContext") {
                diags.Method(method.MethodLoc, method, "invalid first parameter  ->  expect (PipelineContext computeContext)", DiagType.Error);
            }
        } else {
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
            WgslBinding? wgslBinding = null;
            CSharpType csharpType = default;
            if (parameter.IsBindGroupEntry) {
                var bindGroup = parameter.BindGroup;
                bindingTypes.TryGetValue((bindGroup.group, bindGroup.binding), out csharpType);
                wgslBinding = ValidateBinding(parameter, bindings, wgslBindings, diags);
            }
            ValidateParameter(parameter, wgslBinding, csharpType, diags, method.TypeInfos);
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
        private void Shader(SrcLoc srcLoc, in CsShader shader, string message, DiagType type) {
            var error = $"[Shader(\"{shader.path}\")] - {message}";
            diags.Add(new ValidationDiag(srcLoc, error, type));
        }
        
        private void WorkgroupSize(CsWorkgroupSize workgroupSize, string message, DiagType type) {
            var error = $"[WorkgroupSize()] - {message}";
            diags.Add(new ValidationDiag(workgroupSize.attrLoc, error, type));
        }
                
        private void Method(SrcLoc srcLoc, CsMethod method, string message, DiagType type) {
            var error = $"{method.Name} - {message}";
            diags.Add(new ValidationDiag(srcLoc, error, type));
        }

        private void Map(SrcLoc srcLoc, in CsParameter parameter, string message, DiagType type) {
            var bg = parameter.BindGroup;
            var error = $"[Map({bg.group}, {bg.binding})] {parameter.Name} - {message}";
            diags.Add(new ValidationDiag(srcLoc, error, type));
        }
        
        private void Mismatch(SrcLoc loc, in CsParameter parameter, WgslBinding wgslBinding, string message)
        {
            var sb = new StringBuilder();
            sb.Append($"wgsl {message}: C# [");
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
            diags.Map(loc, parameter, sb.ToString(), DiagType.Warn);
        }
        
        private void TypeRequirement(in CsParameter parameter, string expectedType)
        {
            var error = $"[{parameter.ParamAttribute}] {parameter.Name} - Type requirement: {expectedType} - was: {parameter.Type.Name}";
            diags.Add(new ValidationDiag(parameter.TypeLoc, error, DiagType.Error));
        }
        
        private void WgslTypeRequirement(in CsParameter parameter, SrcLoc typeLoc, ValueArray<CsTypeInfo> typeInfos)
        {
            var error = GetWgslTypeError(parameter.Type, typeInfos);
            var msg = $"[{parameter.ParamAttribute}] {parameter.Name} - require WGSL Type (int, float, Vector3, ...) - was: {error}";
            diags.Add(new ValidationDiag(typeLoc, msg, DiagType.Error));
        }
    }
    
    private static WgslBinding? ValidateBinding(
        in CsParameter                      parameter,
        Dictionary<(int,int), CsParameter>  bindings,
        Dictionary<(int,int), WgslBinding>  wgslBindings,
        List<ValidationDiag>                diags)
    {
        var wgslBinding = default(WgslBinding);
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
            if (!wgslBindings.TryGetValue((bindGroup.group,  bindGroup.binding), out wgslBinding)) {
                diags.Map(parameter.BindGroup.attrLoc, parameter, "binding not declared in wgsl", DiagType.Warn);
            } else {
                ValidateBindingType(parameter, wgslBinding, diags);
            }
        }
        return wgslBinding;
    }

    
    private static void ValidateBindingType(in CsParameter parameter, WgslBinding wgslBinding, List<ValidationDiag> diags)
    {
        var paramType = parameter.ParamAttribute.ToString();
        switch (parameter.ParamAttribute)
        {
            case uniform:
            case storage:
                if (paramType != wgslBinding.AddressSpace) {
                    diags.Mismatch(parameter.AttrLoc, parameter, wgslBinding, "binding mismatch");
                }
                return;
            
            // --- Sampler types
            case sampler_NonFiltering:
                paramType = "sampler";  // maps to sampler. no sampler_NonFiltering in WGSL
                goto case sampler;
            case sampler:
            case sampler_comparison:
                if (paramType != wgslBinding.WgslType.Name) {
                    diags.Mismatch(parameter.AttrLoc, parameter, wgslBinding, "type mismatch");
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
                if (paramType != wgslBinding.WgslType.Name) {
                    diags.Mismatch(parameter.AttrLoc, parameter, wgslBinding, "type mismatch");
                }
                else if (parameter.AttrEnum.enum1.Name != wgslBinding.GetGenericNameAt(0)) {
                    diags.Mismatch(parameter.AttrArg0Loc, parameter, wgslBinding, "sample type mismatch");
                }
                return;
            //
            case texture_storage_1d:
            case texture_storage_2d:
            case texture_storage_2d_array:
            case texture_storage_3d:
                var format = WgslTextureFormat.MapWgslStorageFormatToEnumName(wgslBinding.GetGenericNameAt(0));
                if (paramType != wgslBinding.WgslType.Name) {
                    diags.Mismatch(parameter.AttrLoc, parameter, wgslBinding, "type mismatch");
                }
                else if (parameter.AttrEnum.enum1.Name != format) {
                    diags.Mismatch(parameter.AttrArg0Loc, parameter, wgslBinding, "texture format mismatch");
                }
                else if (parameter.AttrEnum.enum2.Name != wgslBinding.GetGenericNameAt(1)) {
                    diags.Mismatch(parameter.AttrArg1Loc, parameter, wgslBinding, "texture storage access mismatch");
                }
                return;
            //
            case texture_depth_multisampled_2d:
            //
            case texture_depth_2d:
            case texture_depth_2d_array:
            case texture_depth_cube:
            case texture_depth_cube_array:
                if (paramType != wgslBinding.WgslType.Name) {
                    diags.Mismatch(parameter.AttrLoc, parameter, wgslBinding, "type mismatch");
                }
                return;
        }
    }
    
    private static CsType GetGenericType(in CsParameter parameter)
    {
        var generics = parameter.Type.Generics;
        return generics.Length == 1 ? generics[0] : default;
    }
    
    private static void ValidateWgslElement(in CsParameter parameter, WgslBinding? wgslBinding, CSharpType bindingType, List<ValidationDiag> diags, ValueArray<CsTypeInfo> typeInfos)
    {
        var type = GetGenericType(parameter);
        if (type.TypeCode.IsWgslType) {
            var accessMode = wgslBinding?.AccessMode;
            if (parameter.IsReadOnlyBuffer && (accessMode == "write" || accessMode == "read_write")) {
                diags.TypeRequirement(parameter, $"access mode '{accessMode}' requires InOutBuffer<>");
            }
            var fields = bindingType.csharpStruct?.fields; 
            if (fields?.Length == 1) {
                var elementType = fields[0].type;
                if (elementType.info.paramType == WgslParamType.DynamicArray) {
                    bindingType = elementType; // use element type if struct contains a single field with dynamic array type 
                }
            }
            if (parameter.IsBindGroupEntry) {
                ValidateLayout(parameter, type, bindingType, parameter.GenericArgLoc, diags);
            }
            return;
        }
        diags.WgslTypeRequirement(parameter, parameter.GenericArgLoc, typeInfos);
    }
    
    private static void ValidateLayout(in CsParameter parameter, in CsType type, in CSharpType bindingType, SrcLoc loc, List<ValidationDiag> diags)
    {
        if (!bindingType.Size.HasValue) {
            return;
        }
        var expectedSize    = bindingType.Size.Value;
        var csharpSize      = type.TypeLayout.Size;
        if (expectedSize != csharpSize) {
            var error = $"[{parameter.ParamAttribute}] {parameter.Name} - Type mismatch: WGSL expects '{bindingType.WgslTypeName}' ({expectedSize} bytes) - was: '{type}' ({csharpSize} bytes)";
            diags.Add(new ValidationDiag(loc, error, DiagType.Error));
        }
    }
    
    private static void ValidateParameter(in CsParameter parameter, WgslBinding? wgslBinding, CSharpType bindingType, List<ValidationDiag> diags, ValueArray<CsTypeInfo> typeInfos)
    {
        var type = parameter.Type;
        switch (parameter.ParamAttribute)
        {
            case uniform:
                if (parameter.IsBuffer) {
                    ValidateWgslElement(parameter, wgslBinding, bindingType, diags, typeInfos);
                    return;
                }
                if (type.TypeCode.IsWgslType) {
                    ValidateLayout(parameter, type, bindingType, parameter.TypeLoc, diags);
                    return;
                }
                diags.WgslTypeRequirement(parameter, parameter.TypeLoc, typeInfos);
                return;
            
            case storage:
                if (parameter.IsBuffer) {
                    ValidateWgslElement(parameter, wgslBinding, bindingType, diags, typeInfos);
                    return;
                }
                diags.TypeRequirement(parameter, "InBuffer<> or InOutBuffer<>");
                return;
            
            case VertexBuffer:
                var slot = parameter.VertexBufferSlot; 
                if (slot < 0 ||slot > 15) {
                    diags.Map(parameter.AttrLoc, parameter, $"slot must be in range 0 - 15. was: {slot}", DiagType.Error);
                }
                if (parameter.IsBuffer) {
                    ValidateWgslElement(in parameter, wgslBinding, default, diags, typeInfos);
                    return;
                }
                diags.TypeRequirement(parameter, "InBuffer<> or InOutBuffer<>");
                return;
            
            case IndexBuffer:
                if (parameter.IsBuffer) {
                    var typeCode = GetGenericType(parameter).TypeCode;
                    if (typeCode == CsTypeCode.UInt16 || typeCode == CsTypeCode.u32) {
                        return;    
                    }
                    diags.TypeRequirement(parameter, "ushort or uint");
                    return;
                }
                diags.TypeRequirement(parameter, "InBuffer<> or InOutBuffer<>");
                return;
            
            // --- Sampler types
            case sampler_NonFiltering:
            case sampler:
            case sampler_comparison:
                if (type.TypeCode != CsTypeCode.GpuSampler) {
                    diags.TypeRequirement(parameter, "GpuSampler");
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
                if (type.TypeCode != CsTypeCode.GpuTextureView) {
                    diags.TypeRequirement(parameter, "GpuTextureView");
                }
                return;
        }
    }
    
    private static string? GetWgslTypeError(CsType type, ValueArray<CsTypeInfo> typeInfos)
    {
        if (type.TypeCode.IsBuffer) {
            var generic = type.Generics;
            if (generic.Length == 1) {
                type = generic[0];
            }
        }
        if (type.TypeCode.IsWgslType) {
            return null;
        }
        var path        = new Stack<string>();
        var errorType   = GetErrorPath(type, path, typeInfos);
        if (path.Count == 0) {
            return errorType.Name;
        }
        return $"{errorType.Name} at {type.Name}.{string.Join(".", path.Reverse())}";
    }
    
    private static CsType GetErrorPath(in CsType type, Stack<string> path, ValueArray<CsTypeInfo> typeInfos)
    {
        if ((type.TypeCode == CsTypeCode.WgslStruct || type.TypeCode == CsTypeCode.CSharpStruct) && path.Count < 10)
        {
            var ti = typeInfos.FindTypeInfo(type.Namespace, type.Name);
            foreach (var field in ti.Fields) {
                path.Push(field.Name);
                var fieldType = GetErrorPath(field.Type, path, typeInfos);
                if (!fieldType.TypeCode.IsWgslType) {
                    return fieldType;
                }
                path.Pop();
            }
        }
        return type;
    }
    
    private static void ValidateShader(CsShader shader, WgslModule module, List<ValidationDiag> diags)
    {
        if (shader.vert    != null) ValidateEntryPoint(shader, "vertex",   shader.vert,    shader.vertLoc,    module, diags);
        if (shader.frag    != null) ValidateEntryPoint(shader, "fragment", shader.frag,    shader.fragLoc,    module, diags);
        if (shader.compute != null) ValidateEntryPoint(shader, "compute",  shader.compute, shader.computeLoc, module, diags);
    }
    
    private static void ValidateEntryPoint(CsShader shader, string stage, string entryName, SrcLoc loc, WgslModule module, List<ValidationDiag> diags)
    {
        var entryPoint = module.EntryPoints.FirstOrDefault(ep => ep.Name == entryName);
        if (entryPoint == null) {
            diags.Shader(loc, shader, $"entry point '{entryName}' not found in WGSL.", DiagType.Error);
            return;
        }
        if (entryPoint.Stage != stage) {
            diags.Shader(loc, shader, $"expect @{stage} attribute on entry point '{entryName}' in WGSL.", DiagType.Error);
            return;
        }
    }
    
    private static void ValidateWorkgroupSize(CsMethod method, WgslModule? module, string? entryName, List<ValidationDiag> diags)
    {
        if (!method.WorkgroupSize.HasValue) {
            return;
        }
        var size = method.WorkgroupSize.Value;
        if (module == null || entryName == null) {
            diags.WorkgroupSize(size, "requires [Shader()] with compute parameter.", DiagType.Error);
            return;
        }
        var entryPoint = module.EntryPoints.FirstOrDefault(ep => ep.Name == entryName);
        if (entryPoint == null) {
            // diags.WorkgroupSize(size, $"entry point '{entryName}' not found in WGSL.", DiagType.Error);
            return;
        }
        var workgroup_size = entryPoint.Attributes.FirstOrDefault(attr => attr.Name == "workgroup_size");
        if (workgroup_size == null) {
            diags.WorkgroupSize(size, "missing @workgroup_size() attribute in WGSL.", DiagType.Error);
            return;
        }
        var args = workgroup_size.Args;
        int arg_0 = 0;
        int arg_1 = 1;
        int arg_2 = 1;
        
        if (args.Length > 0) int.TryParse(args[0], out arg_0);
        if (args.Length > 1) int.TryParse(args[1], out arg_1); 
        if (args.Length > 2) int.TryParse(args[2], out arg_2);
        
        if (arg_0 != size.workgroupCountX || arg_1 != size.workgroupCountY ||  arg_2 != size.workgroupCountZ) {
            var parameters = string.Join(", ", args);
            diags.WorkgroupSize(size, $"Mismatch with @workgroup_size({parameters}) parameters in WGSL.", DiagType.Error);
        }
    }
}

