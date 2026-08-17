// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Friflo.WGPU.Runtime;
using static Friflo.WGPU.Runtime.WebGPU_native;

// ReSharper disable InvertIf
// ReSharper disable TooWideLocalVariableScope
// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.WGPU;

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
    
    private WgpuShaderModule CreateShaderModule(
        Type                type,
        WgpuShader[]        shaders,
        ShaderType          shaderType,
        out string          entry,
        ReadOnlySpan<byte>  shaderLabel)
    {
        entry= null;
        foreach (var shader in shaders) {
            switch (shaderType) {
                case ShaderType.Vertex:     if (shader.vert    != null) entry = shader.vert;    break;
                case ShaderType.Fragment:   if (shader.frag    != null) entry = shader.frag;    break;
                case ShaderType.Compute:    if (shader.compute != null) entry = shader.compute; break;
            }
        }
        var wgslSource = GetFullWgsl(type, shaders, shaderType);
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

    private struct Memory{
        internal byte* ptr;
        internal int   len;
    }
    
    private static ReadOnlySpan<byte> GetFullWgsl(Type type, WgpuShader[] shaders, ShaderType shaderType)
    {
        var resources = new List<Memory>();
        var len = 0;
        foreach (var shader in shaders)
        {
            var addWgsl = shader.frag == null && shader.vert == null               ||
                          shaderType == ShaderType.Vertex   && shader.vert != null ||
                          shaderType == ShaderType.Fragment && shader.frag != null;
            if(!addWgsl) {
                continue;
            }
            if (shaders.Length == 1) {
                return WgpuResource.GetResource(type, shader.path);
            }
            var resource = WgpuResource.GetResource(type, shader.path);
            resources.Add(new Memory {
                ptr = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(resource)),
                len = resource.Length
            });
            len += resource.Length;
        }
        var full = new byte[len];
        int pos = 0;
        foreach (var resource in resources) {
            var src = new ReadOnlySpan<byte>(resource.ptr, resource.len);
            var dst = new Span<byte>(full, pos, src.Length);
            src.CopyTo(dst);
            pos     += src.Length;
        }
        return new ReadOnlySpan<byte>(full);
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
    
    public WgpuComputePipeline CreateComputePipeline(
        Span<WgpuBindGroupLayout>   layouts,
        Type                        type,
        WgpuShader[]                shader,
        ReadOnlySpan<byte>          labelName)
    {
        nativeAllocator.Clear();
        var csModule = CreateShaderModule(type, shader, ShaderType.Compute, out var csEntry, labelName);
        
        var csEntryView = nativeAllocator.StringToNative(csEntry);
        
        fixed (byte*                pLabelName = labelName)
        fixed (WgpuBindGroupLayout* layoutsPtr = layouts)
        {
            var label = WgpuUtils.FromPtrSpan(pLabelName, labelName);
            
            var layoutDesc = new PipelineLayoutDescriptor {
                label                   = label,
                bindGroupLayoutCount    = (uint)layouts.Length,
                bindGroupLayouts        = (BindGroupLayout**)layoutsPtr
            };
            var pipelineLayout = wgpuDeviceCreatePipelineLayout(DevicePtr, &layoutDesc);
            
            var computeDesc = new ComputePipelineDescriptor {
                label       = label,
                layout      = pipelineLayout,
                compute     = new ComputeState {
                    module      = csModule.handle,
                    entryPoint  = csEntryView
                }
            };
            try {
                var handle = wgpuDeviceCreateComputePipeline(DevicePtr, &computeDesc);
                return new WgpuComputePipeline(handle);
            } finally {
                nativeAllocator.Clear();
                if (pipelineLayout != null)     wgpuPipelineLayoutRelease(pipelineLayout);
                csModule.Dispose();
            }
        }
    }
    
    enum ShaderType {
        Vertex,
        Fragment,
        Compute
    }
    
    public WgpuRenderPipeline CreateRenderPipeline(
        Span<WgpuBindGroupLayout>   layouts,
        RenderConfig        		config,
        Type                        type,
        WgpuShader[]                shader,
        ReadOnlySpan<byte>          labelName)
    {
        ref readonly var desc   = ref config.Descriptor;
        nativeAllocator.Clear();
        var vsModule = CreateShaderModule(type, shader, ShaderType.Vertex,   out var vsEntry, labelName);
        var fsModule = CreateShaderModule(type, shader, ShaderType.Fragment, out var fsEntry, labelName);
        
        var vsEntryPtr= nativeAllocator.StringToNative(vsEntry);
        var fsEntryPtr= nativeAllocator.StringToNative(fsEntry);
        
        fixed (byte*                pLabelName      = labelName)
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
                fragmentState.entryPoint    = fsEntryPtr;
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
            vertexState.entryPoint  = vsEntryPtr;
            
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
                vsModule.Dispose();
                fsModule.Dispose();
            }
        }
    }
    

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public WgpuBindGroupLayout GetBindGroupLayout(ulong hashKey)
    {
        layoutCache.TryGetValue(hashKey, out WgpuBindGroupLayout layout);
        return layout;
    }
    
    public void BindGroupLayoutSampler(int binding, SamplerBindingType samplerType)
    {
        bindGroupLayoutEntries[bindGroupLayoutEntriesCount++] = new BindGroupLayoutEntry {
            binding = (uint)binding,
            sampler = new SamplerBindingLayout {
                type    = samplerType
            }
        };
    }
    
    public void BindGroupLayoutTexture(int binding, TextureSampleType sampleType, TextureViewDimension viewDimension, bool multisampled)
    {
        bindGroupLayoutEntries[bindGroupLayoutEntriesCount++] = new BindGroupLayoutEntry {
            binding = (uint)binding,
            texture = new TextureBindingLayout {
                sampleType      = sampleType,
                viewDimension   = viewDimension,
                multisampled    = multisampled ? 1u : 0 
            }
        };
    }
    
    public void BindGroupLayoutStorageTexture(int binding, TextureFormat format, StorageTextureAccess access, TextureViewDimension viewDimension)
    {
        bindGroupLayoutEntries[bindGroupLayoutEntriesCount++] = new BindGroupLayoutEntry {
            binding = (uint)binding,
            storageTexture = new StorageTextureBindingLayout {
                format          = format,
                access          = access,
                viewDimension   = viewDimension
            }
        };
    }
    
    public void BindGroupLayoutUniform(int binding)
    {
        bindGroupLayoutEntries[bindGroupLayoutEntriesCount++] = new BindGroupLayoutEntry {
            binding = (uint)binding,
            buffer  = new BufferBindingLayout {
                type                = BufferBindingType.Uniform,
                hasDynamicOffset    = WgpuUtils.FromBool(true), // true for uniform buffer
                minBindingSize      = 0                         // 0: no validation of minimum size
            }
        };
    }
    
    public void BindGroupLayoutBuffer(int binding, BufferBindingType bindingType)
    {
        bindGroupLayoutEntries[bindGroupLayoutEntriesCount++] = new BindGroupLayoutEntry {
            binding = (uint)binding,
            buffer  = new BufferBindingLayout {
                type                = bindingType,
                hasDynamicOffset    = WgpuUtils.FromBool(false), // true for uniform buffer
                minBindingSize      = 0                          // 0: no validation of minimum size
            }
        };
    }
    
    public WgpuBindGroupLayout GetEmptyBindGroupLayout()
    {
        if (!emptyBindGroupLayout.IsCreated) {
            var emptyLayoutLabel = "empty_bindgroup_layout"u8;
            fixed (byte* labelPtr = emptyLayoutLabel) {
                var desc = new BindGroupLayoutDescriptor {
                    label = WgpuUtils.FromPtrSpan(labelPtr, emptyLayoutLabel)
                };
                var handle = wgpuDeviceCreateBindGroupLayout(DevicePtr, &desc);
                emptyBindGroupLayout = new WgpuBindGroupLayout(handle);
            }
        }
        return emptyBindGroupLayout;
    }
    
    public WgpuBindGroupLayout CreateBindGroupLayout(
        ShaderStage                     visibility,
        ulong                           hashKey,
        ReadOnlySpan<byte>              layoutLabel)
    {
        var entries = bindGroupLayoutEntries;
        var count   = bindGroupLayoutEntriesCount;
        bindGroupLayoutEntriesCount = 0;
        
        for (int n = 0; n < count; ++n) {
            entries[n].visibility = (ulong)visibility;
        }
        BindGroupLayout* handle;
        fixed (byte*                    labelPtr    = layoutLabel)
        fixed (BindGroupLayoutEntry*    entriesPtr  = entries)
        {
            var desc = new BindGroupLayoutDescriptor {
                label       = WgpuUtils.FromPtrSpan(labelPtr, layoutLabel),
                entryCount  = (uint)count,
                entries     = entriesPtr,
            };
            handle = wgpuDeviceCreateBindGroupLayout(DevicePtr, &desc);
            if (handle == null)
                throw new Exception("Failed to create BindGroupLayout. Check your Slot-indexes!");
        }
        // Add new GpuBindGroupLayout to cache
        var layout = new WgpuBindGroupLayout(handle);
        layoutCache.Add(hashKey, layout);
        return layout;
    }
}
