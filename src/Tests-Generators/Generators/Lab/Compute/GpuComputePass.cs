// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Silk.NET.WebGPU;

namespace Friflo.Vectorization.GPU;

public unsafe class GpuComputePass : IDisposable {
    private readonly    GpuEncoder          _encoder;
    public              ComputePassEncoder* Handle { get; }
    private             bool                _hasEnded = false;
    
    public GpuComputePass(GpuEncoder encoder, ComputePassEncoder* handle)
    {
        _encoder = encoder;
        Handle   = handle;
    }
    
    public void Dispose() {
        End(); // Sicherstellen, dass der Pass beendet wurde
        // Den nativen Pass-Encoder freigeben
        if (Handle != null) _encoder.context._wgpu.ComputePassEncoderRelease(Handle);
    }

    public void SetPipeline(GpuComputePipeline pipeline)
    {
        _encoder.context._wgpu.ComputePassEncoderSetPipeline(Handle, pipeline.Handle);
    }
    
    public void DispatchWorkgroups(int workgroupCountX, int workgroupCountY, int workgroupCountZ) {
        _encoder.context._wgpu.ComputePassEncoderDispatchWorkgroups(
            Handle, 
            (uint)workgroupCountX, 
            (uint)workgroupCountY, 
            (uint)workgroupCountZ
        );
    }

    public void End()
    {
        if (!_hasEnded) {
            _encoder.context._wgpu.ComputePassEncoderEnd(Handle);
            _hasEnded = true;
        }
    }

    public void SetBindGroup(int groupIndex, GpuBindGroup bindGroup)
    {
        // Der vierte und fünfte Parameter sind für dynamische Offsets (hier 0/null)
        _encoder.context._wgpu.ComputePassEncoderSetBindGroup(Handle, (uint)groupIndex, bindGroup.Handle, 0, null);
    }
}

public unsafe class GpuBindGroup
{
    public BindGroup* Handle { get; }
    
    public GpuBindGroup(BindGroup* handle) {
        Handle = handle;
    }
}