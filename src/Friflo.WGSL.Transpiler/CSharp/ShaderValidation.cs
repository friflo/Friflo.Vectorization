// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Friflo.WGSL.Transpiler.CodeFixes;
using Friflo.WGSL.Transpiler.WGSL;
using static Friflo.WGSL.Transpiler.CSharp.CsParamAttribute;

// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault
// ReSharper disable InvertIf
// ReSharper disable PossibleMultipleEnumeration
// ReSharper disable InconsistentNaming
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable RedundantJumpStatement
// ReSharper disable ConvertToPrimaryConstructor
namespace Friflo.WGSL.Transpiler.CSharp;



public sealed class ShaderValidation
{
    private  readonly   List<ValidationDiag>    diags       = [];
    private  readonly   ValueArray<CsTypeInfo>  typeInfos;
    private             FieldPath               sourcePath  = new();
    private             FieldPath               targetPath  = new();
    
    public ShaderValidation(ValueArray<CsTypeInfo> typeInfos)
    {
        this.typeInfos = typeInfos;
    }
    
    
    public List<ValidationDiag> Validate(CsMethod method, ImmutableArray<WgslFile> files)
    {
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
            ValidateShader(shader, module);
            typeBuilder.AddModuleType(module, shader.path);
            
            foreach (var binding in module.Bindings) {
                wgslBindings.TryAdd((binding.Group, binding.Binding), binding);
            }
        }
        ValidateWorkgroupSize(method, computeModule, computeEntry);
        
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
                wgslBinding = ValidateBinding(parameter, bindings, wgslBindings);
            }
            ValidateParameter(parameter, wgslBinding, csharpType);
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
    
    
    private WgslBinding? ValidateBinding(
        in CsParameter                      parameter,
        Dictionary<(int,int), CsParameter>  bindings,
        Dictionary<(int,int), WgslBinding>  wgslBindings)
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
                ValidateBindingType(parameter, wgslBinding);
            }
        }
        return wgslBinding;
    }

    
    private void ValidateBindingType(in CsParameter parameter, WgslBinding wgslBinding)
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
    
    private void ValidateWgslElement(in CsParameter parameter, WgslBinding? wgslBinding, CSharpType bindingType)
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
                ValidateLayout(parameter, bindingType, type, parameter.GenericArgLoc);
            }
            return;
        }
        diags.WgslTypeRequirement(parameter, parameter.GenericArgLoc, typeInfos);
    }
    
    private void ValidateLayout(in CsParameter parameter, in CSharpType bindingType, in CsType type, SrcLoc loc)
    {
        if (!bindingType.Size.HasValue) {
            return;
        }
        var expectedSize    = bindingType.Size.Value;
        var csharpSize      = type.TypeLayout.Size;
        if (expectedSize != csharpSize) {
            var error = $"[{parameter.ParamAttribute}] {parameter.Name} - Type mismatch: WGSL expects '{bindingType.WgslTypeName}' ({expectedSize} bytes) - was: '{type}' ({csharpSize} bytes)";
            diags.Add(new ValidationDiag(loc, error, DiagType.Error));
            return;
        }
        sourcePath.Reset();
        targetPath.Reset();
        
        if (!ValidateLayoutType(bindingType, type)) {
            var error = $"[{parameter.ParamAttribute}] {parameter.Name} - Type mismatch: WGSL expects " +
                        $"'{bindingType.WgslTypeName}{sourcePath}' ({sourcePath.type}) - was: '{type}{targetPath}' ({targetPath.type})";
            diags.Add(new ValidationDiag(loc, error, DiagType.Error));
            return;
        }
    }
    
    private bool LeafTypesError(in CSharpType source, in CsType target) {
        sourcePath.type = source.ToString();
        targetPath.type = target.ToString();
        return false;
    }
    
    private bool ValidateLayoutType(CSharpType source, CsType target)
    {
        // If source type is a struct with a single field - validate its field
        while (source.info.typeCode == CsTypeCode.WgslStruct && source.info.paramType != WgslParamType.FixedSizeArray) {
            var fields = source.csharpStruct!.fields;
            if (fields.Length == 1) {
                var field = fields[0];
                sourcePath.Push(field.name);
                source = field.type;
                continue;
            }
            break;
        }
        
        if (source.info.paramType == WgslParamType.FixedSizeArray)
        {
            if (source.Size == target.TypeLayout.Size) {
                if (typeInfos.TryGetTypeInfo(target.Namespace, target.Name, out var typeInfo)) {
                    if (typeInfo.Fields.Length == 1) {
                        var elementType = new CSharpType(source.WgslTypeName, TypeResolution.Resolved,
                            new WgslTypeInfo(source.info.typeCode, WgslParamType.None, 0, source.info.elementType), source.csharpStruct);
                        var targetField = typeInfo.Fields[0];
                        targetPath.Push(targetField.Name);
                        return ValidateLayoutType(elementType, targetField.Type);
                    }
                }
                return true;
            }
            return LeafTypesError(source, target);
        }
        if (source.info.typeCode == CsTypeCode.WgslStruct)
        {
            var sourceFields = source.csharpStruct!.fields;
            if (typeInfos.TryGetTypeInfo(target.Namespace, target.Name, out var typeInfo)) {
                if (sourceFields.Length != typeInfo.Fields.Length) {
                    return false;
                }
                sourcePath.Push();
                targetPath.Push();
                for (var n = 0; n < sourceFields.Length; n++) {
                    var sourceField = sourceFields[n];
                    var targetField = typeInfo.Fields[n];
                    sourcePath.SetTail(sourceField.name);
                    targetPath.SetTail(targetField.Name);
                    if (!ValidateLayoutType(sourceField.type, targetField.Type)) {
                        return false;
                    }
                }
                sourcePath.Pop();
                targetPath.Pop();
            }
            return true;
        }
        if (source.info.typeCode == target.TypeCode) {
            return true;
        }
        // If target type is a struct with a single file validate its field types
        if (target.TypeCode == CsTypeCode.WgslStruct) {
            if (typeInfos.TryGetTypeInfo(target.Namespace, target.Name, out var typeInfo)) {
				if (typeInfo.Fields.Length == 1) {
	                var targetField = typeInfo.Fields[0];
	                targetPath.Push(targetField.Name);
	                return ValidateLayoutType(source, targetField.Type);
            	}
            }
        }
        int scalarCount = 0;
        var dim = source.info.typeCode.Dimension;
        if (CountScalarFields(target, dim.scalarType, ref scalarCount)) {
            if (scalarCount == dim.scalarCount) {
                return true;
            }
        }
        return LeafTypesError(source, target);
    }
    
    private bool CountScalarFields(in CsType target, CsTypeCode scalarType, ref int scalarCount)
    {
        if (target.TypeCode == CsTypeCode.WgslStruct)
        {
            if (typeInfos.TryGetTypeInfo(target.Namespace, target.Name, out var typeInfo))
            {
                foreach (var field in typeInfo.Fields)
                {
                    if (field.Type.TypeCode == CsTypeCode.WgslStruct) {
                        if (!CountScalarFields(field.Type, scalarType, ref scalarCount)) {
                            return false;
                        }
                    }
                    if (field.Type.TypeCode != scalarType) {
                        return false;
                    }
                    scalarCount++;
                }
            }
            return true;
        }
        if (target.TypeCode.IsWgslType) {
            var dim = target.TypeCode.Dimension;
            if (dim.scalarType == scalarType) {
                scalarCount += dim.scalarCount;
                return true;
            }
        }
        return false;
    }
    
    private void ValidateParameter(in CsParameter parameter, WgslBinding? wgslBinding, CSharpType bindingType)
    {
        var type    = parameter.Type;
        
        switch (parameter.ParamAttribute)
        {
            case uniform:
                if (parameter.IsBuffer) {
                    ValidateWgslElement(parameter, wgslBinding, bindingType);
                    return;
                }
                if (type.TypeCode.IsWgslType) {
                    ValidateLayout(parameter, bindingType, type, parameter.TypeLoc);
                    return;
                }
                diags.WgslTypeRequirement(parameter, parameter.TypeLoc, typeInfos);
                return;
            
            case storage:
                if (parameter.IsBuffer) {
                    ValidateWgslElement(parameter, wgslBinding, bindingType);
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
                    ValidateWgslElement(in parameter, wgslBinding, default);
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
    
    private void ValidateShader(CsShader shader, WgslModule module)
    {
        if (shader.vert    != null) ValidateEntryPoint(shader, "vertex",   shader.vert,    shader.vertLoc,    module);
        if (shader.frag    != null) ValidateEntryPoint(shader, "fragment", shader.frag,    shader.fragLoc,    module);
        if (shader.compute != null) ValidateEntryPoint(shader, "compute",  shader.compute, shader.computeLoc, module);
    }
    
    private void ValidateEntryPoint(CsShader shader, string stage, string entryName, SrcLoc loc, WgslModule module)
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
    
    private void ValidateWorkgroupSize(CsMethod method, WgslModule? module, string? entryName)
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

