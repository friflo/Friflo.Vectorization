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
        nativeAllocator.Clear();
        
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
                fragmentState               = desc.FragmentState.Value.GetNative(nativeAllocator);
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
            
            var vertexState = desc.VertexState.GetNative(nativeAllocator);
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
                nativeAllocator.Clear();
                if (pipelineLayout != null)     wgpuPipelineLayoutRelease(pipelineLayout);
            }
        }
    }
    

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public WgpuBindGroupLayout GetBindGroupLayout(ulong hashKey)
    {
        layoutCache.TryGetValue(hashKey, out WgpuBindGroupLayout layout);
        return layout;
    }
    
    public void BindGroupLayoutSampler(SamplerBindingType samplerType)
    {
        bindGroupLayoutEntries[bindGroupLayoutEntriesCount] = new BindGroupLayoutEntry {
            binding = (uint)bindGroupLayoutEntriesCount++,
            sampler = new SamplerBindingLayout {
                type    = samplerType
            }
        };
    }
    
    public void BindGroupLayoutTexture(TextureSampleType sampleType, TextureViewDimension viewDimension, bool multisampled)
    {
        bindGroupLayoutEntries[bindGroupLayoutEntriesCount] = new BindGroupLayoutEntry {
            binding = (uint)bindGroupLayoutEntriesCount++,
            texture = new TextureBindingLayout {
                sampleType      =  sampleType,
                viewDimension   = viewDimension,
                multisampled    = multisampled ? 1u : 0 
            }
        };
    }
    
    public void BindGroupLayoutUniform()
    {
        bindGroupLayoutEntries[bindGroupLayoutEntriesCount] = new BindGroupLayoutEntry {
            binding = (uint)bindGroupLayoutEntriesCount++,
            buffer  = new BufferBindingLayout {
                type                = BufferBindingType.Uniform,
                hasDynamicOffset    = WgpuUtils.FromBool(true), // true for uniform buffer
                minBindingSize      = 0                         // 0: no validation of minimum size
            }
        };
    }
    
    public void BindGroupLayoutBuffer(BufferBindingType bindingType)
    {
        bindGroupLayoutEntries[bindGroupLayoutEntriesCount] = new BindGroupLayoutEntry {
            binding = (uint)bindGroupLayoutEntriesCount++,
            buffer  = new BufferBindingLayout {
                type                = bindingType,
                hasDynamicOffset    = WgpuUtils.FromBool(false), // true for uniform buffer
                minBindingSize      = 0                          // 0: no validation of minimum size
            }
        };
    }
    
    public WgpuBindGroupLayout CreateBindGroupLayout(
        ShaderStage                     visibility,
        ulong                           hashKey,
        ReadOnlySpan<byte>              layoutLabel)
    {
        var entries = bindGroupLayoutEntries;
        for (int n = 0; n < bindGroupLayoutEntriesCount; ++n) {
            entries[n].visibility = (ulong)visibility;
        }
        fixed (byte*                    labelPtr    = layoutLabel)
        fixed (BindGroupLayoutEntry*    entriesPtr  = entries)
        {
            var desc = new BindGroupLayoutDescriptor {
                label       = WgpuUtils.FromPtrSpan(labelPtr, layoutLabel),
                entryCount  = (uint)bindGroupLayoutEntriesCount,
                entries     = entriesPtr,
            };
            bindGroupLayoutEntriesCount = 0;
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
