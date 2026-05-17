

### todo
- Dependency-Tracking - GpuContext.SubmitGraph()
- Visualize Dependency graph (DAC) as Text Graph
- Enable Zero Copy in shadow method. Zero Alloc is already established.
- Establish Zero-Copy - "WriteBuffer" vs. "Mapping" (MappedAtCreation)
- Handle: Out of Memory oder TDR
- Optimization: Implement "Sub-Padding" for small uniforms in GpuTask.AsUniformEntry<>()
- Optimization: Use a global uniform ring buffer with dynamic offsets to eliminate CreateBindGroup() for uniforms.


### done
- Added feature parity of **source generation** for **WebGPU/WGSL** code with **AVX** generated code. Currently not tested.
- Created hardware agnostics **GPU** API - `Gpu*` classes. Same project also contains **AVX** acceleration - `Cpu*` classes.  
  https://www.nuget.org/packages/Friflo.Vectorization.GPU/
- Implemented second project implementing the GPU API - `Wgpu*` classes with binding to **wgpu-native**.  
  This enables support of **DX12**, **Vulkan** and **Metal**.  
  Tested full GPU stack successful on various devices: **Windows 11** x64, **Linux** x64, **Mac Mini M2** arm64 and **Android** arm64.  
  https://www.nuget.org/packages/Friflo.Vectorization.WebGPU/
- Capture UncapturedError in  string lastError. Throw Exception with ThrowIfError() after Task.Finish(), QueueSubmit(), WaitInDebug(), ...
- Check for instance callbacks. static callback methods recommended. All callbacks are now static methods
- Generator: generate unique hash key for cached/deduplicated BindGroupLayout's using FNV-1a as in GpuBuffers
- Layout Merging: Deduplicate BindGroupLayout objects by hashing their descriptors and caching them per device.
    Minimize driver state changes and memory footprint.
- Enable BindGroup caching for passed buffers (storage). Cache capacity: 2 to support double buffering use cases.
- Pass exact count via Uniform Buffer to support buffers with arbitrary length
- Leak-Check for GpuContext.Dispose()
- Ensure Zero GPU Handle/Pointer leaks in unit tests
- task.GpuEncoder() 			creates class GpuEncoder
- encoder.BeginComputePass() 	creates class GpuComputePass
- ctx.CreateBindGroup() 		creates class GpuBindGroup
- Test Shadow method for Zero Alloc
- Support common Dispose pattern (Finalizer & Native State)
- Global Error Callbacks