using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
        if (method.Shader != null) {
            vsModule = "module";
            fsModule = "module";
            shaderModules.Append($"        using var module = device.CreateShaderModule({methodName_GPU}_Shader(), \"{methodName}_Shader\"u8);\n");
            shaderResources.Append($"    private static ReadOnlySpan<byte> {methodName_GPU}_Shader() => WgpuResource.GetResource(typeof({className}), \"Tests-Console.{method.Shader}\");\n");
        } else {
            vsModule = "vsModule";
            fsModule = "fsModule";
            shaderModules.Append($"        using var vsModule = device.CreateShaderModule({methodName_GPU}_VertexShader(),   \"{methodName}_VertexShader\"u8);\n");
            shaderModules.Append($"        using var fsModule = device.CreateShaderModule({methodName_GPU}_FragmentShader(), \"{methodName}_FragmentShader\"u8);\n");
            shaderResources.Append($"    private static ReadOnlySpan<byte> {methodName_GPU}_VertexShader()   => WgpuResource.GetResource(typeof({className}), \"Tests-Console.{method.VertexShader}\");\n");
            shaderResources.Append($"    private static ReadOnlySpan<byte> {methodName_GPU}_FragmentShader() => WgpuResource.GetResource(typeof({className}), \"Tests-Console.{method.FragmentShader}\");\n");
        }
        
        
        // filter / sort parameters use to create bind group layouts & bind groups
        var bindGroups = method.Parameters.Where(p => p.HasBindGroup).ToArray();
        Array.Sort(bindGroups,  (x, y) => {
            int result = x.GroupIndex.CompareTo(y.GroupIndex);
            if (result == 0) {
                result = x.BindingIndex.CompareTo(y.BindingIndex);
            }
            return result;
        });

        var buffers = new StringBuilder();
        var layouts = new List<BindGroupLayout>();
        BindGroupLayout curBindGroupLayout = null;
        
        foreach (var bindGroup in bindGroups) {
            if (curBindGroupLayout == null ||
                curBindGroupLayout.groupIndex != bindGroup.GroupIndex)
            {
                curBindGroupLayout = new BindGroupLayout(bindGroup.GroupIndex); 
                layouts.Add(curBindGroupLayout);
            }
            curBindGroupLayout.bindings.Add(bindGroup);
            var typeName = bindGroup.Type.Identifier.Name;
            if (typeName == "InBuffer" || typeName == "InOutBuffer") {
                if (buffers.Length == 0) {
                    buffers.AppendLine($"        var buffers =\n        GpuBuffers.Create({bindGroup.Name}, nameof({bindGroup.Name}));");
                } else {
                    // buffers.AppendLine($"        var buffers =\n        GpuBuffers.Create({bindGroup.Name}, nameof({bindGroup.Name}));"); // TODO
                }
            }
        }
        
        var bindGroupCaches  = new StringBuilder();
        var layoutKeys       = new StringBuilder();
        var bindGroupLayouts = new StringBuilder();

        foreach (var layout in layouts)
        {
            var groupIndex = layout.groupIndex;
            layoutKeys.Append($"    private const  ulong        {methodName_GPU}_layout_{groupIndex}_Key        =  0x4755;  // TODO\n");
            bindGroupLayouts.Append($"        var layout_{groupIndex} = device.GetBindGroupLayout({methodName_GPU}_layout_{groupIndex}_Key);\n");
            bindGroupLayouts.Append($"        if (!layout_{groupIndex}.IsCreated) {{\n");
            foreach (var binding in layout.bindings) {
                bindGroupLayouts.Append("            ");
                AppendLayout(bindGroupLayouts, binding);
                bindGroupLayouts.Append("\n");
            }
            bindGroupLayouts.Append($"            layout_{groupIndex} = device.CreateBindGroupLayout(ShaderStage.Vertex | ShaderStage.Fragment, {methodName_GPU}_layout_{groupIndex}_Key, \"{methodName}_layout_{groupIndex}\"u8);\n");
            bindGroupLayouts.Append("        }\n");
            bindGroupLayouts.Append($"        layouts[{groupIndex}] = layout_{groupIndex};\n");
            bindGroupLayouts.Append("        \n");
        }


        
        var code =
$$"""
using System;
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
        
        // recorder.RequireRead(vertices); TODO

        ref readonly var pipelineCache = ref recorder.Device.GetPipelineCache({{methodName_GPU}}_ShaderId, config, {{methodName_GPU}}_WgslHash);
        if (!pipelineCache.IsCreated) {
            pipelineCache = ref {{methodName_GPU}}_CreatePipelineCache(recorder.Device, config);
        }
        
        pass_.SetPipeline(pipelineCache.renderPipeline);
    }


    private sealed class {{methodName_GPU}}_Cache : BindGroupCache
    {
        // internal readonly   Dictionary<(nint,nint), WgpuBindGroup>    bindGroup0 = new ();
        
        protected override void Clear() {
            // ReleaseBindGroups(bindGroup0);
        }
    }

    private static readonly int {{methodName_GPU}}_ShaderId            =  ShaderRegistry.NewShaderId("{{methodName_GPU}}");
{{layoutKeys}}
    private static ulong        {{methodName_GPU}}_WgslHash            => 0x1255;  // support Hot-Reload            TODO calculate hash

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ref readonly PipelineCache {{methodName_GPU}}_CreatePipelineCache(WgpuDevice device, RenderConfig config)
    {
        Span<WgpuBindGroupLayout> layouts = stackalloc WgpuBindGroupLayout[{{layouts.Count}}];
{{bindGroupLayouts}}{{shaderModules}}
        var pipeline = device.CreateRenderPipeline(layouts, config, {{vsModule}}, "{{method.VertexEntry}}"u8, {{fsModule}}, "{{method.FragmentEntry}}"u8, "{{methodName}}_pipeline"u8);

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
        switch (binding.ParamAttribute) {
            case CsParamAttribute.BindStorage:
                if (binding.Type.Identifier.Name == "InBuffer") {
                    sb.Append("device.BindGroupLayoutBuffer(BufferBindingType.ReadOnlyStorage);");
                } else {
                    sb.Append("device.BindGroupLayoutBuffer(BufferBindingType.Storage);");
                }
                return;
            case CsParamAttribute.BindUniform:          sb.Append("device.BindGroupLayoutUniform();");                                  return;
            //
            case CsParamAttribute.SamplerFiltering:     sb.Append("device.BindGroupLayoutSampler(SamplerBindingType.Filtering);");      return;
            case CsParamAttribute.SamplerNonFiltering:  sb.Append("device.BindGroupLayoutSampler(SamplerBindingType.NonFiltering);");   return;
            case CsParamAttribute.SamplerComparison:    sb.Append("device.BindGroupLayoutSampler(SamplerBindingType.Comparison);");     return;
            //
        }
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
