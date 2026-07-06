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
    internal readonly   int                 groupIndex = bindGroupGroupIndex;
    internal readonly   List<CsParameter>   bindings    = new();
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
    
    public ShaderEmitter(CsMethod method, string hash)
    {
        this.method     = method;
        methodName      = method.Name;
        methodName_GPU  = $"_{methodName}_GPU{hash}"; 
    }
    
    
    public string Emit(in CsModifier modifier)
    {
        var signature       = GetSignature(method.Parameters, modifier.ParamModifiers);
        var className       = method.DeclaringType.Identifier.Name;
        
        var shaderModules   = new StringBuilder();
        var shaderResources = new StringBuilder();
        string vsModule;
        string fsModule;
        if (method.Source.Shader != null) {
            vsModule = "module";
            fsModule = "module";
            var path = method.Source.Shader;
            shaderModules.Append($"        using var module = device.CreateShaderModule({methodName_GPU}_Shader(), \"{methodName}_Shader\"u8);\n");
            shaderResources.Append($"    private static ReadOnlySpan<byte> {methodName_GPU}_Shader() => WgpuResource.GetResource(typeof({className}), \"{path}\");\n");
        } else {
            vsModule = "vsModule";
            fsModule = "fsModule";
            var vsPath = method.Source.VertexShader;
            var fsPath = method.Source.FragmentShader;
            shaderModules.Append($"        using var vsModule = device.CreateShaderModule({methodName_GPU}_VertexShader(),   \"{methodName}_VertexShader\"u8);\n");
            shaderModules.Append($"        using var fsModule = device.CreateShaderModule({methodName_GPU}_FragmentShader(), \"{methodName}_FragmentShader\"u8);\n");
            shaderResources.Append($"    private static ReadOnlySpan<byte> {methodName_GPU}_VertexShader()   => WgpuResource.GetResource(typeof({className}), \"{vsPath}\");\n");
            shaderResources.Append($"    private static ReadOnlySpan<byte> {methodName_GPU}_FragmentShader() => WgpuResource.GetResource(typeof({className}), \"{fsPath}\");\n");
        }
        
        
        // filter / sort parameters use to create bind group layouts & bind groups
        var bindGroups = method.Parameters.Where(p => p.HasBindGroup).ToArray();
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
            var name = bindGroup.Name;
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
        
        EmitBindGroups(layouts);
        
        // --- set vertex buffers
        bool hasVertexBuffer = false;
        foreach (var parameter in method.Parameters) {
            if (parameter.ParamAttribute != VertexBuffer) continue;
            body.Append($"        pass_.SetVertexBuffer({parameter.Name}, {parameter.BindGroup.group});\n");
            hasVertexBuffer = true;
        }
        if (hasVertexBuffer) body.Append("        \n");
        
        // --- draw
        EmitDraw(body, method);
        
        var foreignUsingNamespaces = GetForeignUsingNamespaces(method);
        var vsEntry = method.Source.VertexEntry;
        var fsEntry = method.Source.FragmentEntry;
        
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
namespace {{method.DeclaringType.Identifier.Namespace}};

public partial {{(modifier.IsClass ? "class" : "struct")}} {{className}}
{
    {{modifier.MethodVisibility}} {{(modifier.IsMethodStatic ? "static " : "")}}partial void {{methodName}}(
{{signature}})
    {
{{buffers}}
        var pass_       = pass.Internal;
		var recorder	= pass_.Recorder;
		recorder.Init({{methodName_GPU}}_ShaderId, "{{methodName}}_encoder"u8);
{{bufferInit}}
        
        ref readonly var pipelineCache = ref recorder.Device.GetPipelineCache({{methodName_GPU}}_ShaderId, config, {{methodName_GPU}}_WgslHash);
        if (!pipelineCache.IsCreated) {
            pipelineCache = ref {{methodName_GPU}}_CreatePipelineCache(recorder.Device, config);
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
    private static ulong        {{methodName_GPU}}_WgslHash            => __WGSL_HASH_PLACEHOLDER__;  // support Hot-Reload

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ref readonly PipelineCache {{methodName_GPU}}_CreatePipelineCache(WgpuDevice device, RenderConfig config)
    {
        Span<WgpuBindGroupLayout> layouts = stackalloc WgpuBindGroupLayout[{{layouts.Count}}];
{{bindGroupLayouts}}{{shaderModules}}
        var pipeline = device.CreateRenderPipeline(layouts, config, {{vsModule}}, "{{vsEntry}}"u8, {{fsModule}}, "{{fsEntry}}"u8, "{{methodName}}_pipeline"u8);

        var bindGroupCache = new {{methodName_GPU}}_Cache();
        return ref device.CreatePipelineCache({{methodName_GPU}}_ShaderId, config, {{methodName_GPU}}_WgslHash, pipeline, layouts, bindGroupCache);
    }
    
{{shaderResources}}
}
""";
        return code;
    }
    
    private void EmitBindGroups(List<BindGroupLayout> layouts)
    {
        foreach (var layout in layouts)
        {
            var index       = layout.groupIndex;
            var bindings    = layout.bindings.Where(binding =>  binding.HasHandle).ToArray();
            var uniforms    = layout.bindings.Where(binding => !binding.HasHandle).ToArray();
            
            ulong layoutKey = LayoutStartHash;
            layoutKey      ^= (ulong)layouts.Count; layoutKey *= Prime;
            layoutKey      ^= (ulong)index;         layoutKey *= Prime;
            
            // --- bind group creation
            body.Append($"        // --- bind group {index}\n");
            if (bindings.Length == 0)
            {
                foreach (var uniform in uniforms) {
                    body.Append($"        pass_.SetBindGroupUniform({index}, ref bindGroupCache.bindGroup{index}, {uniform.Name}, pipelineCache,\"{methodName}_bindGroup{index}\"u8);\n");
                }
                //
                bindGroupMembers.Append($"        internal            WgpuBindGroup bindGroup{index};\n");
                bindGroupClear.Append  ($"            ReleaseBindGroup(ref bindGroup{index});\n");
            } else {
                body.Append($"        var key_{index} = ");
                bindGroupMembers.Append("        internal readonly   Dictionary<");
                if (bindings.Length > 1) {
                    body.Append("(");
                    bindGroupMembers.Append("(");
                }
                foreach (var binding in bindings) {
                    body.Append($"{binding.Name}.Handle, ");
                    bindGroupMembers.Append("nint, ");
                }
                body.Length -= 2;
                bindGroupMembers.Length -= 2;
                if (bindings.Length > 1) {
                    body.Append(")");
                    bindGroupMembers.Append(")");
                }
                body.Append(";\n");
                body.Append($"        if (!bindGroupCache.bindGroup{index}.TryGetValue(key_{index}, out var bindGroup{index})) {{\n");
                foreach (var binding in layout.bindings) {
                    EmitBinding(body, binding);
                }
                body.Append($"            bindGroup{index} = recorder.CreateBindGroup(pipelineCache.layouts[{index}], \"{methodName}_bindGroup{index}\"u8);\n");
                body.Append($"            bindGroupCache.bindGroup{index}.Add(key_{index}, bindGroup{index});\n");
                body.Append( "        }\n");
                foreach (var uniform in uniforms) {
                    body.Append($"        pass_.AddUniform({uniform.Name});\n");    
                }
                if (uniforms.Length > 0) {
                    body.Append($"        pass_.SetBindGroupUniforms({index}, bindGroup{index});\n");
                } else {
                    body.Append($"        pass_.SetBindGroup({index}, bindGroup{index});\n");
                }
                
                //
                bindGroupMembers.Append($", WgpuBindGroup>    bindGroup{index} = new ();\n");
                bindGroupClear.Append  ($"            ReleaseBindGroups(bindGroup{index});\n");
            }
            body.Append($"        \n");
            
            // --- bind group layout creation
            bindGroupLayouts.Append($"        var layout_{index} = device.GetBindGroupLayout({methodName_GPU}_layout_{index}_Key);\n");
            bindGroupLayouts.Append($"        if (!layout_{index}.IsCreated) {{\n");
            foreach (var binding in layout.bindings) {
                bindGroupLayouts.Append("            ");
                layoutKey ^= (ulong)binding.BindGroup.binding;      layoutKey *= Prime;
                layoutKey ^= AddLayout(bindGroupLayouts, binding);  layoutKey *= Prime;
                bindGroupLayouts.Append("\n");
            }
            bindGroupLayouts.Append($"            layout_{index} = device.CreateBindGroupLayout(ShaderStage.Vertex | ShaderStage.Fragment, {methodName_GPU}_layout_{index}_Key, \"{methodName}_layout_{index}\"u8);\n");
            bindGroupLayouts.Append("        }\n");
            bindGroupLayouts.Append($"        layouts[{index}] = layout_{index};\n");
            bindGroupLayouts.Append("        \n");
            
            layoutKeys.Append($"    private const  ulong        {methodName_GPU}_layout_{index}_Key        =  0x{layoutKey:x};\n");
        }
    }
    
    private const ulong LayoutStartHash = 14695981039346656037UL;
    private const ulong Prime           = 1099511628211UL;
    
    private static ulong AddLayout(StringBuilder sb, in CsParameter binding)
    {
        var sampleType = binding.SampleType; // 1 2 3
        
        switch (binding.ParamAttribute) {
            case BindStorage:
                bool isReadonly = binding.IsReadOnlyBuffer;
                                                AppendStorage(sb, isReadonly ? "ReadOnlyStorage" : "Storage");
                                                                                            return isReadonly ? 0x100u : 0x200u;
            case BindUniform:
                bool isBuffer = binding.IsBuffer;
                                                AppendUniform(sb, isBuffer);                return isBuffer   ? 0x300u : 0x400u;
            //
            case SamplerFiltering:              AppendSampler(sb, "Filtering");             return 0x01000;
            case SamplerNonFiltering:           AppendSampler(sb, "NonFiltering");          return 0x02000;
            case SamplerComparison:             AppendSampler(sb, "Comparison");            return 0x03000;
            //
            case texture_1d:                    AppendTexture(sb, sampleType, "D1D");       return 0x04000 + (ulong)sampleType;
            case texture_2d:                    AppendTexture(sb, sampleType, "D2D");       return 0x05000 + (ulong)sampleType;
            case texture_2d_array:              AppendTexture(sb, sampleType, "D2DArray");  return 0x06000 + (ulong)sampleType;
            case texture_3d:                    AppendTexture(sb, sampleType, "D3D");       return 0x07000 + (ulong)sampleType;
            case texture_cube:                  AppendTexture(sb, sampleType, "Cube");      return 0x08000 + (ulong)sampleType;
            case texture_cube_array:            AppendTexture(sb, sampleType, "CubeArray"); return 0x09000 + (ulong)sampleType;
            //
            case texture_multisampled_2d:       AppendTexture(sb, sampleType, "D2D", true); return 0x0a000 + (ulong)sampleType;
            case texture_depth_multisampled_2d: AppendTexture(sb, default,    "D2D", true); return 0x0b000;
            //
            case texture_storage_1d:            AppendTexture(sb, sampleType, "D1D");       return 0x0c000 + (ulong)sampleType;
            case texture_storage_2d:            AppendTexture(sb, sampleType, "D2D");       return 0x0d000 + (ulong)sampleType;
            case texture_storage_2d_array:      AppendTexture(sb, sampleType, "D2DArray");  return 0x0e000 + (ulong)sampleType;
            case texture_storage_3d:            AppendTexture(sb, sampleType, "D3D");       return 0x0f000 + (ulong)sampleType;
            //
            case texture_depth_2d:              AppendTexture(sb, default,    "D2D");       return 0x10000;
            case texture_depth_2d_array:        AppendTexture(sb, default,    "D2DArray");  return 0x11000;
            case texture_depth_cube:            AppendTexture(sb, default,    "Cube");      return 0x12000;
            case texture_depth_cube_array:      AppendTexture(sb, default,    "CubeArray"); return 0x13000;
        }
        return 0;
    }
    
    private static void EmitBinding(StringBuilder body, in CsParameter binding)
    {
        switch (binding.ParamAttribute)
        {
            case BindStorage:
                body.Append($"            recorder.BindGroupEntryBuffer({binding.Name}.Buffer);\n");
                return;
            case BindUniform:
                if (binding.HasHandle) {
                    body.Append($"            recorder.BindGroupEntryBuffer({binding.Name}.Buffer);\n");
                    return;
                }
                var uniformType = binding.Type.Identifier.Name;
                body.Append($"            recorder.BindGroupEntryUniform<{uniformType}>();\n");
                return;
            case SamplerFiltering:
            case SamplerNonFiltering:
            case SamplerComparison:
                body.Append($"            recorder.BindGroupEntrySampler({binding.Name});\n");
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
                body.Append($"            recorder.BindGroupEntryTexture({binding.Name});\n");
                return;
        }
    }
    
    private static void EmitDraw(StringBuilder body, in CsMethod method)
    {
        body.Append("        // --- draw\n");
        if (method.DrawVertexIndex != null) {
            var dvi = method.DrawVertexIndex.Value;
            body.Append($"        pass_.Draw({dvi.vertexCount}, {dvi.instanceCount}, {dvi.firstVertex}, {dvi.firstInstance});\n");
        }

        // attribute: DrawAttribute
        var vertexParam = method.Parameters.FirstOrDefault(p => p.DrawType == CsDrawType.Draw);
        if (vertexParam.Name != null)
        {
            // attribute: DrawInstanceAttribute
            var instanceCount = "1";
            var instanceName = method.Parameters.FirstOrDefault(p => p.DrawType == CsDrawType.DrawInstance).Name;
            if (instanceName != null) {
                instanceCount = $"{instanceName}.Length";
            }
            // attribute: DrawFirstVertexAttribute
            var firstVertex = "0";
            var firstVertexName = method.Parameters.FirstOrDefault(p => p.DrawType == CsDrawType.DrawFirstVertex).Name;
            if (firstVertexName != null) {
                firstVertex = firstVertexName;
            }
            // attribute: DrawFirstInstanceAttribute
            var firstInstance = "0";
            var firstInstanceName = method.Parameters.FirstOrDefault(p => p.DrawType == CsDrawType.DrawFirstInstance).Name;
            if (firstInstanceName != null) {
                firstInstance = firstInstanceName;
            }
            if (vertexParam.ParamAttribute == VertexBuffer) {
                var slot = vertexParam.BindGroup.group; // group is used for slot in [VertexBuffer(slot)]
                body.Append($"        pass_.Draw({vertexParam.Name}, {slot}, config, {instanceCount}, {firstVertex}, {firstInstance});\n");
            } else {
                var name = vertexParam.Name;
                body.Append($"        pass_.Draw({name}.Length, {instanceCount}, {firstVertex}, {firstInstance});\n");
            }
        }
    }
    

    
    private static void AppendStorage(StringBuilder sb, string bindingType)
    {
        sb.Append($"device.BindGroupLayoutBuffer(BufferBindingType.{bindingType});");
    }
    
    private static void AppendUniform(StringBuilder sb, bool isBuffer)
    {
        if (isBuffer) {
            AppendStorage(sb, "Uniform");
        } else {
            sb.Append($"device.BindGroupLayoutUniform();");
        }
    }
    
    private static void AppendSampler(StringBuilder sb, string sampleType)
    {
        sb.Append($"device.BindGroupLayoutSampler(SamplerBindingType.{sampleType});");
    }
    
    private static void AppendTexture(StringBuilder sb, CsSampleType sampleType, string dimension, bool multisampled = false)
    {
        var type = sampleType switch {
            CsSampleType.i32    => "Sint",
            CsSampleType.u32    => "Uint",
            CsSampleType.f32    => "Float",
            _                   => "None"
        };
        var multi = multisampled ? "true" : "false";
        sb.Append($"device.BindGroupLayoutTexture(TextureSampleType.{type}, TextureViewDimension.{dimension}, {multi});");
    }
    
    private static StringBuilder GetSignature(CsParameter[] parameters, CsParamModifier[] modifiers)
    {
        var signature = new StringBuilder();
        
        for (int n = 0; n < parameters.Length; n++)
        {
            var parameter = parameters[n];
            signature.Append("        ");
            var startPos = signature.Length;
            signature.Append(modifiers[n].type);
            signature.Append(parameter.Type.Identifier.Name);
            var generics = parameter.Type.Generics;
            if (generics.Count > 0) {
                signature.Append("<");
                foreach (var generic in generics) {
                    signature.Append(generic.Name);
                    signature.Append(", ");
                }
                signature.Length -= 2;
                signature.Append(">");
            }
            signature.Append(" ");
            var indent = Math.Max(0, 28 - (signature.Length - startPos)); 
            signature.Append(' ', indent);
            signature.Append(parameter.Name);
            signature.Append(",\n");
        }
        signature.Length -= 2;
        return signature;
    }
    
    private static string GetForeignUsingNamespaces(CsMethod method)
    {
        var declaringNamespace  = method.DeclaringType.Identifier.Namespace;
        var namespaces          = new HashSet<string>();
        
        foreach (var parameter in method.Parameters)
        {
            AddNamespace(parameter.Type.Identifier, namespaces, declaringNamespace);
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
    
    private static void AddNamespace(CsTypeIdentifier identifier, HashSet<string> namespaces, string declaringNamespace)
    {
        var ns = identifier.Namespace;
        switch (ns) {
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
