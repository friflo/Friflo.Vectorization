// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Friflo.Vectorization.WebGPU.Runtime;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU;

// -------------- contains methods used by generated kernel methods -------------- 
public sealed unsafe partial  class WgpuDevice
{
    public CommandRecorder Recorder
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)] [StackTraceHidden]
        get {
            var context = Context;
            if (context == null) MissingContextException();
            ValidateThreadSafety(context);
            return (CommandRecorder)context;
        }
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)] [StackTraceHidden] [DoesNotReturn]
    private void MissingContextException() {
        throw new InvalidOperationException($"Missing Device Context: '{Label}'. Call:  using var context = device.BeginContext();  before calling kernel method.");
    }
    
    
    // --- effectSlots
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref WgpuEffect GetEffect(int slot, ulong wgslHash)
    {
        var slots = effectSlots;
        if (slot < slots.Length) {
            ref var effect = ref slots[slot];
            if (effect.wgslHash == wgslHash) {
                return ref effect;
            }
        }
        return ref MissingEffect;
    }
    
    private static WgpuEffect MissingEffect;
    
    public ref WgpuEffect CreateEffect(
        int                     kernelId,
        ulong                   wgslHash,
        WgpuComputePipeline     pipeline,
        WgpuBindGroupLayout     bufferLayout,
        WgpuBindGroupLayout     uniformLayout)
    {
        var slots = effectSlots;
        if (kernelId >= slots.Length) {
            var newSlots = new WgpuEffect[Math.Max(2 * slots.Length, kernelId + 1)];
            Array.Copy(slots, newSlots, slots.Length);
            slots = effectSlots = newSlots;
        }
        slots[kernelId] = new WgpuEffect(kernelId, wgslHash, pipeline, bufferLayout, uniformLayout);
        return ref slots[kernelId];
    }
    
    public void UpdateBufferCache(int slot, WgpuBindGroup bindGroup, ulong hash) {
        effectSlots[slot].bufferCache.Update(bindGroup, hash);
    }
    
    public WgpuShaderModule CreateShaderModule(ReadOnlySpan<byte> wgslSource, ReadOnlySpan<byte> shaderLabel)
    {
        fixed (byte* pShaderBytes = wgslSource)
        fixed (byte* labelPtr = shaderLabel)
        {
            // create descriptor
            var wgslDesc = new ShaderSourceWGSL {    	// was: new ShaderModuleWGSLDescriptor
                code    = WgpuUtils.FromPtrSpan(pShaderBytes, wgslSource),
                chain   = new ChainedStruct {
                    sType   = SType.ShaderSourceWGSL	// was: SType.ShaderModuleWgsldescriptor
                }
            };
            var desc = new ShaderModuleDescriptor {
                label       = WgpuUtils.FromPtrSpan(labelPtr, shaderLabel),
                nextInChain = (ChainedStruct*)&wgslDesc,
            };
            // Compile shader in driver
            var handle = wgpuDeviceCreateShaderModule(DevicePtr, &desc);
            errorHandler.ThrowOnError();
            return new WgpuShaderModule(handle);
        }
    }
    
    public WgpuComputePipeline CreateComputePipeline(
        WgpuShaderModule    module,
        WgpuBindGroupLayout bufferLayout,
        WgpuBindGroupLayout uniformLayout,
        ReadOnlySpan<byte>  entryPoint)
    {
        Span<WgpuBindGroupLayout> layouts = stackalloc WgpuBindGroupLayout[2];
        layouts[0] = bufferLayout;
        layouts[1] = uniformLayout;
        
        fixed (byte*                pEntryPoint = entryPoint)
        fixed (WgpuBindGroupLayout*  layoutsPtr  = layouts)
        {
            var label = WgpuUtils.FromPtrSpan(pEntryPoint, entryPoint);
            var layoutDesc = new PipelineLayoutDescriptor {
                label                   = label,
                bindGroupLayoutCount    = 2,
                bindGroupLayouts        = (BindGroupLayout**)layoutsPtr
            };
            var pipelineLayout = wgpuDeviceCreatePipelineLayout(DevicePtr, &layoutDesc);
            try {
                var computeDesc = new ComputePipelineDescriptor {
                    label       = label,
                    layout      = pipelineLayout,
                    compute     = new ComputeState {	// was: new ProgrammableStageDescriptor
                        module      = module.handle,
                        entryPoint  = WgpuUtils.FromPtrSpan(pEntryPoint, entryPoint)
                    }
                };
                var handle = wgpuDeviceCreateComputePipeline(DevicePtr, &computeDesc);
                return new WgpuComputePipeline(handle);
            } finally {
                if (pipelineLayout != null) wgpuPipelineLayoutRelease(pipelineLayout);
                if (module.handle  != null) wgpuShaderModuleRelease(module.handle);
            }
        }
    }
    

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public WgpuBindGroupLayout GetBindGroupLayout(ulong hashKey)
    {
        layoutCache.TryGetValue(hashKey, out WgpuBindGroupLayout layout);
        return layout;
    }

    public WgpuBindGroupLayout CreateBindGroupLayout(ReadOnlySpan<WgpuLayoutEntry> entries, ShaderStage visibility, bool dynamicOffset, ulong hashKey, ReadOnlySpan<byte> layoutLabel)
    {
        Span<BindGroupLayoutEntry> nativeEntries = stackalloc BindGroupLayoutEntry[entries.Length];
        
        for (int i = 0; i < entries.Length; i++) {
            nativeEntries[i] = new BindGroupLayoutEntry {
                binding         = (uint)entries[i].Binding,
                visibility      = (ulong)visibility,
                buffer          = new BufferBindingLayout {
                    type                = entries[i].Type,
                    hasDynamicOffset    = WgpuUtils.FromBool(dynamicOffset),    // true for uniform buffer
                    minBindingSize      = 0                                     // 0: no validation of minimum size
                }
            };
        }
        fixed (byte*                    labelPtr    = layoutLabel)
        fixed (BindGroupLayoutEntry*    entriesPtr  = nativeEntries)
        {
            var desc = new BindGroupLayoutDescriptor {
                label       = WgpuUtils.FromPtrSpan(labelPtr, layoutLabel),
                entryCount  = (uint)nativeEntries.Length,
                entries     = entriesPtr,
            };
            var handle = wgpuDeviceCreateBindGroupLayout(DevicePtr, &desc);
            if (handle == null)
                throw new Exception("Failed to create BindGroupLayout. Check your Slot-indexes!");
            
            // Add new GpuBindGroupLayout to cache
            var layout = new WgpuBindGroupLayout(handle);
            layoutCache.Add(hashKey, layout);
            return layout;
        }
    }
}
