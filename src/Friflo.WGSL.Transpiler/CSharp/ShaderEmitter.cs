using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static Friflo.WGSL.Transpiler.CSharp.CsParamAttribute;

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

public static class ShaderEmitter
{
    public static string EmitShader(bool staticMethod, CsMethod method, string hash)
    {
        var methodName      = method.Name;
        var signature       = GetSignature(method.Parameters);
        var methodName_GPU  = "_" + methodName + "_GPU";
        var className       = method.DeclaringType.Identifier.Name;
        
        var shaderModules   = new StringBuilder();
        var shaderResources = new StringBuilder();
        string vsModule;
        string fsModule;
        if (method.Source.Shader != null) {
            vsModule = "module";
            fsModule = "module";
            shaderModules.Append($"        using var module = device.CreateShaderModule({methodName_GPU}_Shader(), \"{methodName}_Shader\"u8);\n");
            shaderResources.Append($"    private static ReadOnlySpan<byte> {methodName_GPU}_Shader() => WgpuResource.GetResource(typeof({className}), \"Tests-Console.{method.Source.Shader}\");\n");
        } else {
            vsModule = "vsModule";
            fsModule = "fsModule";
            shaderModules.Append($"        using var vsModule = device.CreateShaderModule({methodName_GPU}_VertexShader(),   \"{methodName}_VertexShader\"u8);\n");
            shaderModules.Append($"        using var fsModule = device.CreateShaderModule({methodName_GPU}_FragmentShader(), \"{methodName}_FragmentShader\"u8);\n");
            shaderResources.Append($"    private static ReadOnlySpan<byte> {methodName_GPU}_VertexShader()   => WgpuResource.GetResource(typeof({className}), \"Tests-Console.{method.Source.VertexShader}\");\n");
            shaderResources.Append($"    private static ReadOnlySpan<byte> {methodName_GPU}_FragmentShader() => WgpuResource.GetResource(typeof({className}), \"Tests-Console.{method.Source.FragmentShader}\");\n");
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
            if (bindGroup.IsBuffer) {           // TODO  1. remove var buffers (GpuBuffers)
                if (buffers.Length == 0) {      // TODO  2. only validate buffer parameters
                    buffers.AppendLine($"        var buffers =\n        GpuBuffers.Create({name}, nameof({name}));");
                } else {
                    // buffers.AppendLine($"        var buffers =\n        GpuBuffers.Create({name}, nameof({name}));");
                }
                var requireType = bindGroup.IsReadOnlyBuffer ? "RequireRead     " : "RequireReadWrite";
                bufferInit.Append($"\n        recorder.{requireType}({name});");
            }
        }
        
        // - bind group creation
        var bindGroupBlock      = new StringBuilder();
        var bindGroupMembers    = new StringBuilder();
        var bindGroupClear      = new StringBuilder();
        // - bind group layout creation
        var layoutKeys          = new StringBuilder();
        var bindGroupLayouts    = new StringBuilder();

        foreach (var layout in layouts)
        {
            var index       = layout.groupIndex;
            var bindings    = layout.bindings.Where(binding =>  binding.HasHandle).ToArray();
            var uniforms    = layout.bindings.Where(binding => !binding.HasHandle).ToArray();
            
            // --- bind group creation
            bindGroupBlock.Append($"        // --- bind group {index}\n");
            if (bindings.Length == 0)
            {
                foreach (var uniform in uniforms) {
                    bindGroupBlock.Append($"        pass_.SetBindGroupUniform({index}, ref bindGroupCache.bindGroup{index}, {uniform.Name}, pipelineCache,\"{methodName}_bindGroup{index}\"u8);\n");
                }
                //
                bindGroupMembers.Append($"        internal            WgpuBindGroup bindGroup{index};\n");
                bindGroupClear.Append  ($"            ReleaseBindGroup(ref bindGroup{index});\n");
            } else {
                bindGroupBlock.Append($"        var key_{index} = ");
                bindGroupMembers.Append("        internal readonly   Dictionary<");
                if (bindings.Length > 1) {
                    bindGroupBlock.Append("(");
                    bindGroupMembers.Append("(");
                }
                foreach (var binding in bindings) {
                    bindGroupBlock.Append($"{binding.Name}.Handle, ");
                    bindGroupMembers.Append("nint, ");
                }
                bindGroupBlock.Length -= 2;
                bindGroupMembers.Length -= 2;
                if (bindings.Length > 1) {
                    bindGroupBlock.Append(")");
                    bindGroupMembers.Append(")");
                }
                bindGroupBlock.Append(";\n");
                bindGroupBlock.Append($"        if (!bindGroupCache.bindGroup{index}.TryGetValue(key_{index}, out var bindGroup{index})) {{\n");
                bindGroupBlock.Append($"            bindGroup{index} = recorder.CreateBindGroup(pipelineCache.layouts[{index}], \"{methodName}_bindGroup{index}\"u8);\n");
                bindGroupBlock.Append($"            bindGroupCache.bindGroup{index}.Add(key_{index}, bindGroup{index});\n");
                bindGroupBlock.Append( "        }\n");
                bindGroupBlock.Append($"        pass_.SetBindGroup({index}, bindGroup{index});\n");
                
                //
                bindGroupMembers.Append($", WgpuBindGroup>    bindGroup{index} = new ();\n");
                bindGroupClear.Append  ($"            ReleaseBindGroups(bindGroup{index});\n");
            }
            bindGroupBlock.Append($"        \n");
            
            // --- bind group layout creation
            var layoutKey = index;                                                                         // TODO  implement key calculation
            layoutKeys.Append($"    private const  ulong        {methodName_GPU}_layout_{index}_Key        =  0x0{layoutKey};  // TODO\n");
            bindGroupLayouts.Append($"        var layout_{index} = device.GetBindGroupLayout({methodName_GPU}_layout_{index}_Key);\n");
            bindGroupLayouts.Append($"        if (!layout_{index}.IsCreated) {{\n");
            foreach (var binding in layout.bindings) {
                bindGroupLayouts.Append("            ");
                AppendLayout(bindGroupLayouts, binding);
                bindGroupLayouts.Append("\n");
            }
            bindGroupLayouts.Append($"            layout_{index} = device.CreateBindGroupLayout(ShaderStage.Vertex | ShaderStage.Fragment, {methodName_GPU}_layout_{index}_Key, \"{methodName}_layout_{index}\"u8);\n");
            bindGroupLayouts.Append("        }\n");
            bindGroupLayouts.Append($"        layouts[{index}] = layout_{index};\n");
            bindGroupLayouts.Append("        \n");
        }
        
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

namespace {{method.DeclaringType.Identifier.Namespace}};

public partial class {{className}}
{
    public {{(staticMethod ? "static " : "")}}partial void {{methodName}}(
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

{{bindGroupBlock}}
    }


    private sealed class {{methodName_GPU}}_Cache : BindGroupCache
    {
{{bindGroupMembers}}
        protected override void Clear() {
{{bindGroupClear}}        }
    }

    private static readonly int {{methodName_GPU}}_ShaderId            =  ShaderRegistry.NewShaderId("{{methodName_GPU}}");
{{layoutKeys}}
    private static ulong        {{methodName_GPU}}_WgslHash            => 0x1255;  // support Hot-Reload            TODO calculate hash

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ref readonly PipelineCache {{methodName_GPU}}_CreatePipelineCache(WgpuDevice device, RenderConfig config)
    {
        Span<WgpuBindGroupLayout> layouts = stackalloc WgpuBindGroupLayout[{{layouts.Count}}];
{{bindGroupLayouts}}{{shaderModules}}
        var pipeline = device.CreateRenderPipeline(layouts, config, {{vsModule}}, "{{method.Source.VertexEntry}}"u8, {{fsModule}}, "{{method.Source.FragmentEntry}}"u8, "{{methodName}}_pipeline"u8);

        var bindGroupCache = new {{methodName_GPU}}_Cache();
        return ref device.CreatePipelineCache({{methodName_GPU}}_ShaderId, config, {{methodName_GPU}}_WgslHash, pipeline, layouts, bindGroupCache);
    }
    
{{shaderResources}}
}
""";
        return code;
    }
    
    private static void AppendLayout(StringBuilder sb, in CsParameter binding)
    {
        var sampleType = binding.SampleType;
        
        switch (binding.ParamAttribute) {
            case BindStorage:   AppendStorage(sb, binding.IsReadOnlyBuffer ? "ReadOnlyStorage" : "Storage");    return;
            case BindUniform:   AppendUniform(sb, binding.IsBuffer);                                            return;
            //
            case SamplerFiltering:              AppendSampler(sb, "Filtering");             return;
            case SamplerNonFiltering:           AppendSampler(sb, "NonFiltering");          return;
            case SamplerComparison:             AppendSampler(sb, "Comparison");            return;
            //
            case texture_1d:                    AppendTexture(sb, sampleType, "D1D");       return;
            case texture_2d:                    AppendTexture(sb, sampleType, "D2D");       return;
            case texture_2d_array:              AppendTexture(sb, sampleType, "D2DArray");  return;
            case texture_3d:                    AppendTexture(sb, sampleType, "D3D");       return;
            case texture_cube:                  AppendTexture(sb, sampleType, "Cube");      return;
            case texture_cube_array:            AppendTexture(sb, sampleType, "CubeArray"); return;
            //
            case texture_multisampled_2d:       AppendTexture(sb, sampleType, "D2D", true); return;
            case texture_depth_multisampled_2d: AppendTexture(sb, default,    "D2D", true); return;
            //
            case texture_storage_1d:            AppendTexture(sb, sampleType, "D1D");       return;
            case texture_storage_2d:            AppendTexture(sb, sampleType, "D2D");       return;
            case texture_storage_2d_array:      AppendTexture(sb, sampleType, "D2DArray");  return;
            case texture_storage_3d:            AppendTexture(sb, sampleType, "D3D");       return;
            //
            case texture_depth_2d:              AppendTexture(sb, default,    "D2D");       return;
            case texture_depth_2d_array:        AppendTexture(sb, default,    "D2DArray");  return;
            case texture_depth_cube:            AppendTexture(sb, default,    "Cube");      return;
            case texture_depth_cube_array:      AppendTexture(sb, default,    "CubeArray"); return;
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
    
    private static StringBuilder GetSignature(CsParameter[] parameters)
    {
        var signature = new StringBuilder();
        foreach (var parameter in parameters)
        {
            signature.Append("        ");        
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
            signature.Append(parameter.Name);
            signature.Append(",\n");
        }
        signature.Length -= 2;
        return signature;
    }
}
