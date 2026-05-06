

### todo
- Dependency-Tracking - GpuContext.SubmitGraph()
- Visualize Dependency graph (DAC) as Text Graph
- Enable Zero Copy in shadow method. Zero Alloc is already established.
- Establish Zero-Copy - "WriteBuffer" vs. "Mapping" (MappedAtCreation)
- Check for instance callbacks. static callback methods recommended
- Handle: Out of Memory oder TDR
- Capture UncapturedError in  string lastError. Throw Exception with ThrowIfError() after Task.Finish(), QueueSubmit(), WaitInDebug(), ...
- Generator: generate unique hash key for cached/deduplicated BindGroupLayout's using FNV-1a as in GpuBuffers
- Optimization: Implement "Sub-Padding" for small uniforms in GpuTask.AsUniformEntry<>()


### done
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