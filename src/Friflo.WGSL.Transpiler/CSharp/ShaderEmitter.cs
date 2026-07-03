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
        
        var bindGroupCaches = new StringBuilder();

        foreach (var layout in layouts)
        {
            foreach (var binding in layout.bindings)
            {
                
                
            }
        }

        var methodName      = method.Name;
        var signature       = GetSignature(method.Parameters);
        var methodName_GPU  = "_" + methodName + "_GPU";
        
        var code =
$$"""
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.GPU.Runtime;
using Friflo.Vectorization.WebGPU;

namespace {{method.DeclaringType.Identifier.Namespace}};

public partial class {{method.DeclaringType.Identifier.Name}}
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

    private static readonly int {{methodName_GPU}}_ShaderId            =  ShaderRegistry.NewShaderId("TextureTestShader");
    private const  ulong        {{methodName_GPU}}_layout_0_Key        =  0x4755;  // unique key set by Generator   TODO calculate key
    private static ulong        {{methodName_GPU}}_WgslHash            => 0x1255;  // support Hot-Reload            TODO calculate hash

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ref readonly PipelineCache {{methodName_GPU}}_CreatePipelineCache(WgpuDevice device, RenderConfig config)
    {
    /*  var layout_0 = device.GetBindGroupLayout({{methodName_GPU}}_layout_0_Key);
        if (!layout_0.IsCreated) {
            device.BindGroupLayoutUniform();
            device.BindGroupLayoutSampler(SamplerBindingType.Filtering);
            device.BindGroupLayoutTexture(TextureSampleType.Float, TextureViewDimension.D2D, false);
            layout_0 = device.CreateBindGroupLayout(ShaderStage.Vertex | ShaderStage.Fragment, {{methodName_GPU}}_layout_0_Key, "TextureTest_layout_0"u8);
        }
        using var vsModule = device.CreateShaderModule({{methodName_GPU}}_VertexShader(),   "TextureTest_VertexShader"u8);
        using var fsModule = device.CreateShaderModule({{methodName_GPU}}_FragmentShader(), "TextureTest_FragmentShader"u8);
        
        Span<WgpuBindGroupLayout> layouts = stackalloc WgpuBindGroupLayout[1];
        layouts[0] = layout_0;

        var pipeline = device.CreateRenderPipeline(layouts, config, vsModule, "main"u8, fsModule, "main"u8, "TextureTest_pipeline"u8);

        var bindGroupCache = new {{methodName_GPU}}_Cache();
        return ref device.CreatePipelineCache({{methodName_GPU}}_ShaderId, config, {{methodName_GPU}}_WgslHash, pipeline, layouts, bindGroupCache); */
        throw new  NotImplementedException();
    }
    // private static ReadOnlySpan<byte> {{methodName_GPU}}_VertexShader()   => WgpuResource.GetResource(typeof(TexturedCube), "Tests-Console.shaders.basic.vert.wgsl");
    // private static ReadOnlySpan<byte> {{methodName_GPU}}_FragmentShader() => WgpuResource.GetResource(typeof(TexturedCube), "Tests-Console.shaders.sampleTextureMixColor.frag.wgsl");
}
""";
        return code;
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
