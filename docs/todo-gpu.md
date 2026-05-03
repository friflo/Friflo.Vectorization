

## todo
- Leak-Check for GpuContext.Dispose()
- Dependency-Tracking - GpuContext.SubmitGraph()
- Enable Zero Copy in shadow method. Zero Alloc is already established.
- Establish Zero-Copy - "WriteBuffer" vs. "Mapping" (MappedAtCreation)
- Check for instance callbacks. static callback methods recommended
- Handle: Out of Memory oder TDR
- Capture UncapturedError in  string lastError. Throw Exception with ThrowIfError() after Task.Finish(), QueueSubmit(), WaitInDebug(), ...

- task.GpuEncoder() 			creates class GpuEncoder		- DONE
- encoder.BeginComputePass() 	creates class GpuComputePass  	- DONE
- ctx.CreateBindGroup() 		creates class GpuBindGroup    	- DONE
- Test Shadow method for Zero Alloc 							- DONE
- Support common Dispose pattern (Finalizer & Native State)		- DONE
- Global Error Callbacks:										- DONE

current TODO: GPU Leaks
- task.CreateBindGroup
- ShaderModule & Pipelines (GpuEffectSlot)
- CommandBuffer (Submit)