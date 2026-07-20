// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static Friflo.WGSL.Transpiler.CSharp.CsParamAttribute;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertIfStatementToConditionalTernaryExpression
// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault
// ReSharper disable MergeIntoPattern
// ReSharper disable InvertIf
// ReSharper disable RedundantSwitchExpressionArms
// ReSharper disable MergeIntoLogicalPattern
// ReSharper disable InconsistentNaming
namespace Friflo.WGSL.Transpiler.CSharp;


internal sealed class BindGroupLayout(int bindGroupGroupIndex)
{
    internal readonly   int                 groupIndex  = bindGroupGroupIndex;
    internal readonly   List<CsParameter>   bindings    = [];
}

public sealed class ShaderEmitter
{
    private readonly    string          methodName;
    private readonly    string          methodName_GPU;
    private readonly    CsMethod        method;
    
    private readonly    StringBuilder   body                = new ();
    // - bind group creation
    private readonly    StringBuilder   bindGroupMembers    = new ();
    private readonly    StringBuilder   bindGroupClear      = new ();
    // - bind group layout creation
    private readonly    StringBuilder   layoutKeys          = new ();
    private readonly    StringBuilder   bindGroupLayouts    = new ();
    
    public ShaderEmitter(CsMethod method)
    {
        this.method     = method;
        methodName      = method.Name;
        methodName_GPU  = $"_{methodName}_GPU{method.Hash}"; 
    }
    
    
    public string Emit(ulong wgslHash, bool hasErrors)
    {
        var header = GetMethodHeader();
        if (hasErrors || method.Parameters.Length == 0) {
            return header + " { }\n}\n";
        }

        var shaderResources = new StringBuilder();
        
        shaderResources.Append($"    private static readonly WgpuShader[] {methodName_GPU}_Shaders = [\n");
        foreach (var shader in method.Shaders) {
            shaderResources.Append($"        new(\"{shader.path}\"");
            if (shader.vert != null) shaderResources.Append($", vert: \"{shader.vert}\"");
            if (shader.frag != null) shaderResources.Append($", frag: \"{shader.frag}\"");
            shaderResources.Append("),\n");
        }
        shaderResources.Append($"    ];\n");
        
        // filter / sort parameters use to create bind group layouts & bind groups
        var bindGroups = method.Parameters.Where(p => p.IsBindGroupEntry).ToArray();
        Array.Sort(bindGroups,  (x, y) => {
            int result = x.BindGroup.group.CompareTo(y.BindGroup.group);
            if (result == 0) {
                result = x.BindGroup.binding.CompareTo(y.BindGroup.binding);
            }
            return result;
        });

        var buffers     = new StringBuilder();
        var bufferInit  = new StringBuilder();
        var layouts     = new List<BindGroupLayout>();
        BindGroupLayout curBindGroupLayout = null;
        
        foreach (var bindGroup in bindGroups)
        {
            if (curBindGroupLayout == null ||
                curBindGroupLayout.groupIndex != bindGroup.BindGroup.group)
            {
                curBindGroupLayout = new BindGroupLayout(bindGroup.BindGroup.group); 
                layouts.Add(curBindGroupLayout);
            }
            curBindGroupLayout.bindings.Add(bindGroup);
            /* if (bindGroup.IsBuffer) {           // TODO  1. remove var buffers (GpuBuffers)
                if (buffers.Length == 0) {         // TODO  2. only validate buffer parameters
                    buffers.AppendLine($"        var buffers =\n        GpuBuffers.Create({name}, nameof({name}));");
                } else {
                    // buffers.AppendLine($"        var buffers =\n        GpuBuffers.Create({name}, nameof({name}));");
                }
                var requireType = bindGroup.IsReadOnlyBuffer ? "RequireRead     " : "RequireReadWrite";
                bufferInit.Append($"\n        recorder.{requireType}({name});");
            } */
        }

        foreach (var parameter in method.Parameters)
        {
            if (!parameter.IsBuffer) continue;
            var requireType = parameter.IsReadOnlyBuffer ? "RequireRead     " : "RequireReadWrite";
            bufferInit.Append($"\n        recorder.{requireType}({parameter.Name});");
        }
        
        var layoutCount = bindGroups.Length == 0 ? 0 : bindGroups.Last().BindGroup.group + 1; 
        var layoutArray = new BindGroupLayout[layoutCount];
        foreach (var layout in layouts) {
            layoutArray[layout.groupIndex] = layout;
        }
        
        EmitBindGroups(layoutArray);
        
        // --- set index / vertex buffers
        bool addedBuffer = false;
        foreach (var parameter in method.Parameters) {
            switch (parameter.ParamAttribute) {
                case VertexBuffer:
                    body.Append($"        pass_.SetVertexBuffer({parameter.Name}, {parameter.VertexBufferSlot});\n");
                    addedBuffer = true;
                    break;
                case IndexBuffer:
                    var generics = parameter.Type.Generics;
                    if (generics.Length == 1) {
                        var indexFormat = generics[0].Name == "ushort" ? "Uint16" : "Uint32";
                        body.Append($"        pass_.SetIndexBuffer({parameter.Name}, IndexFormat.{indexFormat});\n");
                        addedBuffer = true;
                    }
                    break;
            }
        }
        if (addedBuffer) body.Append("        \n");
        
        // --- draw
        EmitDraw(body, method);
        
        var className   = method.DeclaringType.Name;
        var passName    = method.Parameters[0].Name;
        var configName  = method.Parameters[1].Name;

        // language=csharp
        var code =
$$"""
{{header}}
    {
{{buffers}}
        var pass_       = {{passName}}.Internal;
		var recorder	= pass_.Recorder;
		recorder.Init({{methodName_GPU}}_ShaderId, "{{methodName}}_encoder"u8);
{{bufferInit}}
        
        ref readonly var pipelineCache = ref recorder.Device.GetPipelineCache({{methodName_GPU}}_ShaderId, {{configName}}, {{methodName_GPU}}_WgslHash);
        if (!pipelineCache.IsCreated) {
            pipelineCache = ref {{methodName_GPU}}_CreatePipelineCache(recorder.Device, {{configName}});
        }
        pass_.SetPipeline(pipelineCache.renderPipeline);
        
        var bindGroupCache = ({{methodName_GPU}}_Cache)pipelineCache.bindGroupCache;

{{body}}    }

    private sealed class {{methodName_GPU}}_Cache : BindGroupCache
    {
{{bindGroupMembers}}
        protected override void Clear() {
{{bindGroupClear}}        }
    }

    private static readonly int {{methodName_GPU}}_ShaderId            =  ShaderRegistry.NewShaderId("{{methodName}}");
{{layoutKeys}}
    private static ulong        {{methodName_GPU}}_WgslHash            => 0x{{wgslHash:x}}UL;  // support Hot-Reload

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ref readonly PipelineCache {{methodName_GPU}}_CreatePipelineCache(WgpuDevice device, RenderConfig config)
    {
        Span<WgpuBindGroupLayout> layouts = stackalloc WgpuBindGroupLayout[{{layoutArray.Length}}];
{{bindGroupLayouts}}        var pipeline = device.CreateRenderPipeline(layouts, config, typeof({{className}}), {{methodName_GPU}}_Shaders, "{{methodName}}_pipeline"u8);

        var bindGroupCache = new {{methodName_GPU}}_Cache();
        return ref device.CreatePipelineCache({{methodName_GPU}}_ShaderId, config, {{methodName_GPU}}_WgslHash, pipeline, layouts, bindGroupCache);
    }
    
{{shaderResources}}
}
""";
        return code;
    }
    
    private void EmitBindGroupCaching(int group, CsParameter[] resources)
    {
        if (resources.Length == 0) {
            bindGroupMembers.Append($"        internal            WgpuBindGroup bindGroup_{group};\n");
            bindGroupClear.Append  ($"            ReleaseBindGroup(ref bindGroup_{group});\n");
            return;
        }
        // emit key, Dictionary<(nint, ...), WgpuBindGroup> and release cached bind groups 
        body.Append($"        var key_{group} = ");
        bindGroupMembers.Append("        internal readonly   Dictionary<");
        if (resources.Length > 1) {
            body.Append("(");
            bindGroupMembers.Append("(");
        }
        foreach (var resource in resources) {
            body.Append($"{resource.Name}.Handle, ");
            bindGroupMembers.Append("nint, ");
        }
        body.Length -= 2;
        bindGroupMembers.Length -= 2;
        if (resources.Length > 1) {
            body.Append(")");
            bindGroupMembers.Append(")");
        }
        body.Append(";\n");
        bindGroupMembers.Append($", WgpuBindGroup> bindGroup_{group} = new ();\n");
        bindGroupClear.Append  ($"            ReleaseBindGroups(bindGroup_{group});\n");
    }
    
    private void EmitBindGroup(int group, List<CsParameter> bindings, CsParameter[] uniforms)
    {
        // does bind group contains only uniforms?
        if (bindings.Count == uniforms.Length)
        {
            // case: bind group is cached with a simple WgpuBindGroup field
            if (uniforms.Length == 1) {
                var uniform = uniforms[0];
                var binding = uniform.BindGroup.binding;
                body.Append($"        pass_.SetBindGroupUniform({group}, {binding}, ref bindGroupCache.bindGroup_{group}, {uniform.Name}, pipelineCache,\"{methodName}_bindGroup_{group}\"u8);\n");
            } else {
                body.Append($"        if (!bindGroupCache.bindGroup_{group}.IsCreated) {{\n");
                foreach (var uniform in uniforms) {
                    body.Append($"            recorder.BindGroupEntryUniform<{uniform.Type.Name}>({uniform.BindGroup.binding});\n");
                }
                body.Append($"            bindGroupCache.bindGroup_{group} = recorder.CreateBindGroup(pipelineCache.layouts[{group}], \"{methodName}_bindGroup_{group}\"u8);\n");
                body.Append("        }\n");
                foreach (var uniform in uniforms) {
                    body.Append($"        pass_.AddUniform({uniform.Name});\n");
                }
                body.Append($"        pass_.SetBindGroupUniforms({group}, bindGroupCache.bindGroup_{group});\n");
            }
            return;
        }
        // case: bind group is cached via with a Dictionary<(nint, ...), WgpuBindGroup>
        body.Append($"        if (!bindGroupCache.bindGroup_{group}.TryGetValue(key_{group}, out var bindGroup_{group})) {{\n");
        foreach (var binding in bindings) {
            EmitBinding(body, binding);
        }
        body.Append($"            bindGroup_{group} = recorder.CreateBindGroup(pipelineCache.layouts[{group}], \"{methodName}_bindGroup_{group}\"u8);\n");
        body.Append($"            bindGroupCache.bindGroup_{group}.Add(key_{group}, bindGroup_{group});\n");
        body.Append( "        }\n");
        foreach (var uniform in uniforms) {
            body.Append($"        pass_.AddUniform({uniform.Name});\n");    
        }
        if (uniforms.Length == 0) {
            body.Append($"        pass_.SetBindGroup({group}, bindGroup_{group});\n");
        } else {
            body.Append($"        pass_.SetBindGroupUniforms({group}, bindGroup_{group});\n");
        }
    }
    
    private void EmitBindGroups(BindGroupLayout[] layouts)
    {
        for (int group = 0; group < layouts.Length; group++)
        {
            var layout = layouts[group];
            if (layout == null) {
                bindGroupLayouts.Append($"        layouts[{group}] = device.GetEmptyBindGroupLayout();\n");
                bindGroupLayouts.Append($"        \n");
                continue;
            }
            var bindings    = layout.bindings;
            var resources   = bindings.Where(binding =>  binding.IsResource).ToArray(); // bindings with a Handle
            var uniforms    = bindings.Where(binding => !binding.IsResource).ToArray(); // only uniform bindings
            
            body.Append($"        // --- bind group {group}\n");
            
            // --- bind group creation & set bind group
            EmitBindGroupCaching(group, resources);

            EmitBindGroup(group, bindings, uniforms);

            body.Append($"        \n");
            
            // --- bind group layout creation
            ulong layoutKey = LayoutStartHash;
            layoutKey      ^= (ulong)layouts.Length; layoutKey *= Prime;
            layoutKey      ^= (ulong)group;          layoutKey *= Prime;
            
            bindGroupLayouts.Append($"        var layout_{group} = device.GetBindGroupLayout({methodName_GPU}_layout_{group}_Key);\n");
            bindGroupLayouts.Append($"        if (!layout_{group}.IsCreated) {{\n");
            foreach (var binding in bindings) {
                bindGroupLayouts.Append("            ");
                layoutKey ^= (ulong)binding.BindGroup.binding;      layoutKey *= Prime;
                layoutKey ^= AddLayout(bindGroupLayouts, binding);  layoutKey *= Prime;
                bindGroupLayouts.Append("\n");
            }
            bindGroupLayouts.Append($"            layout_{group} = device.CreateBindGroupLayout(ShaderStage.Vertex | ShaderStage.Fragment, {methodName_GPU}_layout_{group}_Key, \"{methodName}_layout_{group}\"u8);\n");
            bindGroupLayouts.Append("        }\n");
            bindGroupLayouts.Append($"        layouts[{group}] = layout_{group};\n");
            bindGroupLayouts.Append("        \n");
            
            layoutKeys.Append($"    private const  ulong        {methodName_GPU}_layout_{group}_Key        =  0x{layoutKey:x};\n");
        }
    }
    
    private const ulong LayoutStartHash = 14695981039346656037UL;
    private const ulong Prime           = 1099511628211UL;
    
    private static ulong AddLayout(StringBuilder sb, in CsParameter binding)
    {
        var index       = binding.BindGroup.binding;
        var sampleType  = binding.AttrEnum.enum1; // WGSL enum:  ST    1 2 3
        var format      = binding.AttrEnum.enum1; // WGPU enum:  TextureFormat
        var access      = binding.AttrEnum.enum2; // WGSL enum:  TSA   1 2 3
        
        switch (binding.ParamAttribute) {
            case storage:
                bool isReadonly = binding.IsReadOnlyBuffer;
                                                AppendStorage(sb, index, isReadonly ? "ReadOnlyStorage" : "Storage");
                                                                                                    return isReadonly ? 0x100u : 0x200u;
            case uniform:
                bool isBuffer = binding.IsBuffer;
                                                AppendUniform(sb, index, isBuffer);                 return isBuffer   ? 0x300u : 0x400u;
            //
            case sampler:                       AppendSampler(sb, index, "Filtering");              return 0x01000;
            case sampler_NonFiltering:          AppendSampler(sb, index, "NonFiltering");           return 0x02000;
            case sampler_comparison:            AppendSampler(sb, index, "Comparison");             return 0x03000;
            //
            case texture_1d:                    Texture(sb, index, sampleType, "D1D");              return 0x04000 + sampleType.Value;
            case texture_2d:                    Texture(sb, index, sampleType, "D2D");              return 0x05000 + sampleType.Value;
            case texture_2d_array:              Texture(sb, index, sampleType, "D2DArray");         return 0x06000 + sampleType.Value;
            case texture_3d:                    Texture(sb, index, sampleType, "D3D");              return 0x07000 + sampleType.Value;
            case texture_cube:                  Texture(sb, index, sampleType, "Cube");             return 0x08000 + sampleType.Value;
            case texture_cube_array:            Texture(sb, index, sampleType, "CubeArray");        return 0x09000 + sampleType.Value;
            //
            case texture_multisampled_2d:       TextureMultisampled(sb, index, sampleType, "D2D");  return 0x0a000 + sampleType.Value;
            case texture_depth_multisampled_2d: TextureMultisampled(sb, index, null,       "D2D");  return 0x0b000;
            //
            case texture_storage_1d:        TextureStorage(sb, index, format, access, "D1D");       return 0x0c000 + format.Value + (access.Value << 8);
            case texture_storage_2d:        TextureStorage(sb, index, format, access, "D2D");       return 0x0d000 + format.Value + (access.Value << 8);
            case texture_storage_2d_array:  TextureStorage(sb, index, format, access, "D2DArray");  return 0x0e000 + format.Value + (access.Value << 8);
            case texture_storage_3d:        TextureStorage(sb, index, format, access, "D3D");       return 0x0f000 + format.Value + (access.Value << 8);
            //  
            case texture_depth_2d:              TextureDepth(sb, index, "D2D");                     return 0x10000;
            case texture_depth_2d_array:        TextureDepth(sb, index, "D2DArray");                return 0x11000;
            case texture_depth_cube:            TextureDepth(sb, index, "Cube");                    return 0x12000;
            case texture_depth_cube_array:      TextureDepth(sb, index, "CubeArray");               return 0x13000;
        }
        return 0;
    }
    
    private static void EmitBinding(StringBuilder body, in CsParameter binding)
    {
        var index = binding.BindGroup.binding;
        switch (binding.ParamAttribute)
        {
            case storage:
                body.Append($"            recorder.BindGroupEntryBuffer({index}, {binding.Name}.Buffer);\n");
                return;
            case uniform:
                if (binding.IsResource) {
                    body.Append($"            recorder.BindGroupEntryBuffer({index}, {binding.Name}.Buffer);\n");
                    return;
                }
                var uniformType = binding.Type.Name;
                body.Append($"            recorder.BindGroupEntryUniform<{uniformType}>({index});\n");
                return;
            case sampler:
            case sampler_NonFiltering:
            case sampler_comparison:
                body.Append($"            recorder.BindGroupEntrySampler({index}, {binding.Name});\n");
                return;
            case texture_1d:
            case texture_2d:
            case texture_2d_array:
            case texture_3d:
            case texture_cube:
            case texture_cube_array:
            case texture_multisampled_2d:
            case texture_depth_multisampled_2d:
            case texture_storage_1d:
            case texture_storage_2d:
            case texture_storage_2d_array:
            case texture_storage_3d:
            case texture_depth_2d:
            case texture_depth_2d_array:
            case texture_depth_cube:
            case texture_depth_cube_array:
                body.Append($"            recorder.BindGroupEntryTexture({index}, {binding.Name});\n");
                return;
        }
    }
    
    private static void EmitDraw(StringBuilder body, in CsMethod method)
    {
        body.Append("        // --- draw\n");
        if (method.DrawVertexIndex != null) {
            var dvi = method.DrawVertexIndex.Value;
            body.Append($"        pass_.Draw(new DrawArgs({dvi.vertexCount}, {dvi.instanceCount}, {dvi.firstVertex}, {dvi.firstInstance}));\n");
            return;
        }

        var methodParameters = method.Parameters;
        // attribute: DrawAttribute
        var drawParam = methodParameters.FirstOrDefault(p => p.DrawAttribute == CsDrawAttribute.Draw);
        if (drawParam.Name == null) {
        	return;
		}
        var (drawArgsParameter, isArray) = GetDrawArgsParameter(methodParameters);
        var (isIndirect, isIndexed)      = IsIndirectBufferParameter(drawParam);

        var drawArgs = isIndirect ? "new DrawIndirectArgs()" : "new DrawArgs()";
        
        var indent = "";
        if (isArray) {
			// case: Instanced Batching  (aka: CPU-driven Multi-Draw or Batch-Rendering)
            indent = "    ";
            body.Append($"        foreach(var {drawArgsParameter}Item in {drawArgsParameter}) {{\n");
            drawArgsParameter += "Item";
        }
        if (drawArgsParameter != null) {
            drawArgs = drawArgsParameter;
        } else if (!isIndirect) {
			// attribute: DrawInstanceAttribute
            var instanceName = methodParameters.FirstOrDefault(p => p.DrawAttribute == CsDrawAttribute.DrawInstance).Name;
            if (instanceName != null) {
            	drawArgs = $"DrawArgs.InstanceCount({instanceName})";
        	}
		}
        var paramName = drawParam.Name;
        var suffix    = isIndirect ? "Indirect" : "";
        
        switch (drawParam.ParamAttribute) {
            case storage:
            case uniform:
                var drawMethod  = isIndexed  ? "DrawIndexed" : "Draw";
                body.Append($"{indent}        pass_.{drawMethod}{suffix}({paramName}, {drawArgs});\n");
                break;
            case VertexBuffer:
                var slot        = drawParam.VertexBufferSlot;
                var configName  = method.Parameters[1].Name;
                body.Append($"{indent}        pass_.Draw{suffix}({paramName}, {slot}, {configName}, {drawArgs});\n");
                break;
            case IndexBuffer:
                body.Append($"{indent}        pass_.DrawIndexed{suffix}({paramName}, {drawArgs});\n");
                break;
        }
        if (isArray) {
        	body.Append("        }\n");
    	}
    }
    
    private static (bool isIndirect, bool isIndexed) IsIndirectBufferParameter(in CsParameter drawParam)
    {
        if (drawParam.IsBuffer)
        {
            var generics = drawParam.Type.Generics;
            if (generics.Length == 1) {
                switch (generics[0].Name) {
                    case "Indirect":        return (true, false);
                    case "IndexedIndirect": return (true, true);
                }
            }
        }
        return (false, false);
    }

    
    private static (string name, bool isArray) GetDrawArgsParameter(ValueArray<CsParameter> parameters)
    {
        foreach (var p in parameters) {
            switch (p.Type.Name) {
                case "DrawArgs":          return (p.Name, p.Type.IsArray);
                case "DrawIndirectArgs":  return (p.Name, false); // never a CPU-array loop for Indirect
                case "Span":
                case "ReadOnlySpan":      
                    return (p.Name, true); 
            }
        }
        return default;
    }
    
    private static void AppendStorage(StringBuilder sb, int binding, string bindingType)
    {
        sb.Append($"device.BindGroupLayoutBuffer({binding}, BufferBindingType.{bindingType});");
    }
    
    private static void AppendUniform(StringBuilder sb, int binding, bool isBuffer)
    {
        if (isBuffer) {
            AppendStorage(sb, binding, "Uniform");
        } else {
            sb.Append($"device.BindGroupLayoutUniform({binding});");
        }
    }
    
    private static void AppendSampler(StringBuilder sb, int binding, string sampleType)
    {
        sb.Append($"device.BindGroupLayoutSampler({binding}, SamplerBindingType.{sampleType});");
    }
    
    private static string GetSampleTypeEnum( CsEnum sampleType) =>
         // WGSL enum:  ST
        sampleType.Name switch {
            "i32"   => "Sint",
            "u32"   => "Uint",
            "f32"   => "Float",
            _       => "None"
        };
    
    private static void Texture(StringBuilder sb, int binding, CsEnum sampleType, string dimension)
    {
        var type = GetSampleTypeEnum(sampleType);
        sb.Append($"device.BindGroupLayoutTexture({binding}, TextureSampleType.{type}, TextureViewDimension.{dimension}, false);");
    }
    
    private static void TextureMultisampled(StringBuilder sb, int binding, CsEnum? sampleType, string dimension)
    {
        var type = sampleType == null ? "Depth" : GetSampleTypeEnum(sampleType.Value);
        sb.Append($"device.BindGroupLayoutTexture({binding}, TextureSampleType.{type}, TextureViewDimension.{dimension}, true);");
    }
    
    private static void TextureDepth(StringBuilder sb, int binding, string dimension)
    {
        sb.Append($"device.BindGroupLayoutTexture({binding}, TextureSampleType.Depth, TextureViewDimension.{dimension}, false);");
    }
    
    private static void TextureStorage(StringBuilder sb, int binding, CsEnum format, CsEnum access, string dimension)
    {
        // WGSL enum:  TSA
        var tsa = access.Name switch  {
            "read"          => "ReadOnly",
            "write"         => "WriteOnly",
            "read_write"    => "ReadWrite",
            _               => "BindingNotUsed"
        };
        sb.Append($"device.BindGroupLayoutStorageTexture({binding}, TextureFormat.{format}, StorageTextureAccess.{tsa}, TextureViewDimension.{dimension});");
    }
    
    private string GetMethodHeader()
    {
        var signature   = GetSignature(method);
        var modifier    = method.Modifier;
        var className   = method.DeclaringType.Name;
        var foreignUsingNamespaces = GetForeignUsingNamespaces(method);
        
        // language=csharp
        var code =
$$"""
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.GPU.Runtime;
using Friflo.Vectorization.WebGPU;
using Friflo.Vectorization.WebGPU.Runtime;
{{foreignUsingNamespaces}}
namespace {{method.DeclaringType.Namespace}};

public partial {{(modifier.IsClass ? "class" : "struct")}} {{className}}
{
    {{modifier.MethodVisibility}} {{(modifier.IsMethodStatic ? "static " : "")}}partial void {{methodName}}{{signature}}
""";
        return code;
    }
    
    private static string GetSignature(CsMethod method)
    {
        if (method.Parameters.Length == 0) {
            return "()";
        }
        var signature = new StringBuilder();
        signature.Append("(\n");
        
        for (int n = 0; n < method.Parameters.Length; n++)
        {
            var parameter = method.Parameters[n];
            signature.Append("        ");
            var startPos = signature.Length;
            signature.Append(method.Modifier.ParamModifiers[n].type);
            signature.Append(parameter.Type.Name);
            var generics = parameter.Type.Generics;
            if (generics.Length > 0) {
                signature.Append("<");
                foreach (var generic in generics) {
                    signature.Append(generic.Name);
                    signature.Append(", ");
                }
                signature.Length -= 2;
                signature.Append(">");
            }
            if (parameter.Type.IsArray) {
                signature.Append("[]");    
            }
            signature.Append(" ");
            var indent = Math.Max(0, 28 - (signature.Length - startPos)); 
            signature.Append(' ', indent);
            signature.Append(parameter.Name);
            signature.Append(",\n");
        }
        signature.Length -= 2;
        signature.Append(")");
        return signature.ToString();
    }
    
    private static string GetForeignUsingNamespaces(CsMethod method)
    {
        var declaringNamespace  = method.DeclaringType.Namespace;
        var namespaces          = new HashSet<string>();
        
        foreach (var parameter in method.Parameters)
        {
            AddNamespace(parameter.Type, namespaces, declaringNamespace);
            foreach (var generic in parameter.Type.Generics) {
                AddNamespace(generic, namespaces, declaringNamespace);
            }
        }
        if (namespaces.Count == 0) {
            return "";
        }
        var sb = new StringBuilder();
        var array = namespaces.ToArray();
        Array.Sort(array);
        foreach (var ns in array) {
            sb.Append("using ");
            sb.Append(ns);
            sb.Append(";\n");
        }
        return sb.ToString();
    }
    
    private static void AddNamespace(in CsType identifier, HashSet<string> namespaces, string declaringNamespace)
    {
        var ns = identifier.Namespace;
        switch (ns) {
            case "":    // global namespace
            case "System":
            case "System.Numerics":
            case "Friflo.Vectorization.GPU":
            case "Friflo.Vectorization.GPU.Runtime":
            case "Friflo.Vectorization.WebGPU":
            case "Friflo.Vectorization.WebGPU.Runtime":
                return;
            default:
                if (ns == declaringNamespace) {
                    return;
                }
                namespaces.Add(ns);
                break;
        }
    }
}
