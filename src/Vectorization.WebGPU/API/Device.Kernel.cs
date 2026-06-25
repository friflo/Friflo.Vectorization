// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Friflo.Vectorization.WebGPU.Runtime;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// ReSharper disable InvertIf
// ReSharper disable TooWideLocalVariableScope
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
    
    
    // --------------------- computeEffectSlots ---------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref WgpuComputeEffect GetComputeEffect(int slot, ulong wgslHash)
    {
        var slots = computeEffectSlots;
        if (slot < slots.Length) {
            ref var effect = ref slots[slot];
            if (effect.wgslHash == wgslHash) {
                return ref effect;
            }
        }
        return ref MissingComputeEffect;
    }
    
    private static WgpuComputeEffect MissingComputeEffect;
    
    public ref WgpuComputeEffect CreateComputeEffect(
        int                     kernelId,
        ulong                   wgslHash,
        WgpuComputePipeline     pipeline,
        WgpuBindGroupLayout     bufferLayout,
        WgpuBindGroupLayout     uniformLayout)
    {
        var slots = computeEffectSlots;
        if (kernelId >= slots.Length) {
            slots = WgpuUtils.Resize(ref computeEffectSlots, kernelId + 1);
        }
        slots[kernelId] = new WgpuComputeEffect(kernelId, wgslHash, pipeline, bufferLayout, uniformLayout);
        return ref slots[kernelId];
    }
    
    // TODO REMOVE
    public void UpdateComputeCache(ref WgpuComputeEffect effect, WgpuBindGroup bindGroup, ulong hash) {
        effect.computeBufferCache.Update(bindGroup, hash);
    }
    
    // --------------------------------------------------------------
    public WgpuShaderModule CreateShaderModule(ReadOnlySpan<byte> wgslSource, ReadOnlySpan<byte> shaderLabel)
    {
        fixed (byte* pShaderBytes = wgslSource)
        fixed (byte* labelPtr = shaderLabel)
        {
            // create descriptor
            var wgslDesc = new ShaderSourceWGSL {
                code    = WgpuUtils.FromPtrSpan(pShaderBytes, wgslSource),
                chain   = new ChainedStruct {
                    sType   = SType.ShaderSourceWGSL
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
            
            var computeDesc = new ComputePipelineDescriptor {
                label       = label,
                layout      = pipelineLayout,
                compute     = new ComputeState {
                    module      = module.handle,
                    entryPoint  = WgpuUtils.FromPtrSpan(pEntryPoint, entryPoint)
                }
            };
            try {
                var handle = wgpuDeviceCreateComputePipeline(DevicePtr, &computeDesc);
                return new WgpuComputePipeline(handle);
            } finally {
                if (pipelineLayout != null) wgpuPipelineLayoutRelease(pipelineLayout);
                if (module.handle  != null) wgpuShaderModuleRelease(module.handle);
            }
        }
    }
    
    public WgpuRenderPipeline CreateRenderPipeline(
        Span<WgpuBindGroupLayout>   layouts,
        RenderConfig        		config,
        WgpuShaderModule            vsModule,
        ReadOnlySpan<byte>          vertexEntryPoint,
        WgpuShaderModule            fsModule,
        ReadOnlySpan<byte>          fragmentEntryPoint,
        ReadOnlySpan<byte>          labelName)
    {
        ref readonly var desc   = ref config.Descriptor;
        var allocator           = new NativeAllocator();
        
        fixed (byte*                pLabelName      = labelName)
        fixed (byte*                pVertexEntry    = vertexEntryPoint)
        fixed (byte*                pFragmentEntry  = fragmentEntryPoint)
        fixed (WgpuBindGroupLayout* layoutsPtr      = layouts)
        {
            var label = WgpuUtils.FromPtrSpan(pLabelName, labelName);
            
            var layoutDesc = new PipelineLayoutDescriptor {
                label                   = label,
                bindGroupLayoutCount    = (uint)layouts.Length,
                bindGroupLayouts        = (BindGroupLayout**)layoutsPtr
            };
            var pipelineLayout = wgpuDeviceCreatePipelineLayout(DevicePtr, &layoutDesc);
            
            FragmentState* pFragmentState = null;
            FragmentState   fragmentState;
            if (desc.FragmentState.HasValue) {
                fragmentState               = desc.FragmentState.Value.GetNative(allocator);
                fragmentState.module        = fsModule.handle;
                fragmentState.entryPoint    = WgpuUtils.FromPtrSpan(pFragmentEntry, fragmentEntryPoint);
                pFragmentState = &fragmentState;
            }
            
            DepthStencilState* pDepthStencilState = null;
            DepthStencilState   depthStencilState;
            if (desc.DepthStencilState.HasValue) {
                depthStencilState   = desc.DepthStencilState.Value.GetNative();
                pDepthStencilState  = &depthStencilState;
            }
            
            var vertexState = desc.VertexState.GetNative(allocator);
            vertexState.module      = vsModule.handle;
            vertexState.entryPoint  = WgpuUtils.FromPtrSpan(pVertexEntry, vertexEntryPoint);
            
            var renderDesc = new RenderPipelineDescriptor {
                label       = label,
                layout      = pipelineLayout,
                vertex      = vertexState,
                fragment    = pFragmentState,
                primitive   = desc.PrimitiveState.GetNative(),
                multisample = desc.MultisampleState.GetNative(),
                depthStencil= pDepthStencilState
            };
            try {
                var handle = wgpuDeviceCreateRenderPipeline(DevicePtr, &renderDesc);
                return new WgpuRenderPipeline(handle);
            } finally {
                allocator.FreePointers();
                if (pipelineLayout != null)     wgpuPipelineLayoutRelease(pipelineLayout);
                if (vsModule.handle != null) {
                    wgpuShaderModuleRelease(vsModule.handle);
                }
                if (fsModule.handle != null && fsModule.handle != vsModule.handle) {
                    wgpuShaderModuleRelease(fsModule.handle);
                }
            }
        }
    }
    

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public WgpuBindGroupLayout GetBindGroupLayout(ulong hashKey)
    {
        layoutCache.TryGetValue(hashKey, out WgpuBindGroupLayout layout);
        return layout;
    }

    public WgpuBindGroupLayout CreateBindGroupLayout(
        ReadOnlySpan<WgpuLayoutEntry>   entries,
        ShaderStage                     visibility,
        bool                            dynamicOffset,          // TODO remove - obsolete
        ulong                           hashKey,
        ReadOnlySpan<byte>              layoutLabel)
    {
        Span<BindGroupLayoutEntry> nativeEntries = stackalloc BindGroupLayoutEntry[entries.Length];
        
        for (int i = 0; i < entries.Length; i++)
        {
            var entry   = entries[i];
            var buffer  = new BufferBindingLayout();
            var sampler = new SamplerBindingLayout();
            var texture = new TextureBindingLayout();
            switch (entry.type)
            {
                case LayoutEntryType.Buffer:
                    buffer.type             = entry.BufferType;
                    buffer.hasDynamicOffset = WgpuUtils.FromBool(false);
                    buffer.minBindingSize   = 0;                            // 0: no validation of minimum size
                    break;
                case LayoutEntryType.Uniform:
                    buffer.type             = entry.BufferType;
                    buffer.hasDynamicOffset = WgpuUtils.FromBool(true);     // true for uniform buffer
                    buffer.minBindingSize   = 0;                            // 0: no validation of minimum size
                    break;
                case LayoutEntryType.Sampler:
                    sampler.type            = entry.samplerType;
                    break;
                case LayoutEntryType.Texture    :
                    texture.sampleType      = entry.sampleType;
                    texture.viewDimension   = entry.viewDimension;
                    texture.multisampled    = (uint)(entry.multisampled ? 1 : 0);
                    break;
            }
            nativeEntries[i] = new BindGroupLayoutEntry {
                binding     = (uint)entry.Binding,
                visibility  = (ulong)visibility,
                buffer      = buffer,
                sampler     = sampler,
                texture     = texture
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
