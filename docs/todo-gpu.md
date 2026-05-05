

### todo
- Dependency-Tracking - GpuContext.SubmitGraph()
- Enable Zero Copy in shadow method. Zero Alloc is already established.
- Establish Zero-Copy - "WriteBuffer" vs. "Mapping" (MappedAtCreation)
- Check for instance callbacks. static callback methods recommended
- Handle: Out of Memory oder TDR
- Capture UncapturedError in  string lastError. Throw Exception with ThrowIfError() after Task.Finish(), QueueSubmit(), WaitInDebug(), ...
- Layout Merging: Deduplicate BindGroupLayout objects by hashing their descriptors and caching them per device.
    Minimize driver state changes and memory footprint.
    (Generator todo: the Layout key can be calculated already in the Generator)


### done
- Leak-Check for GpuContext.Dispose()
- Ensure Zero GPU Handle/Pointer leaks in unit tests
- task.GpuEncoder() 			creates class GpuEncoder
- encoder.BeginComputePass() 	creates class GpuComputePass
- ctx.CreateBindGroup() 		creates class GpuBindGroup
- Test Shadow method for Zero Alloc
- Support common Dispose pattern (Finalizer & Native State)
- Global Error Callbacks