// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;


// ReSharper disable InconsistentNaming
// ReSharper disable UseNullPropagation
// ReSharper disable MergeIntoPattern
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU.Runtime;

[EditorBrowsable(EditorBrowsableState.Never)]
public struct GpuBuffers
{
    public  readonly    int         length;
    public              ulong       hash; // uses FNV-1a derivative hashing
    public  readonly    GpuDevice   device;
    private readonly    bool        areSpans;
    private readonly    string      firstParam;
    private readonly    ComputeMode computeMode;
    
    public              bool        ComputeGPU  => computeMode == ComputeMode.GPU;
    public              bool        ComputeSIMD => computeMode == ComputeMode.SIMD;

    
    private const ulong Prime       = 0x100000001b3;
    private const ulong OffsetBasis = 0xcbf29ce484222325;
    
    private GpuBuffers(ComputeMode computeMode, int length) {
        this.length         = length;
        this.areSpans       = true;
        this.computeMode    = computeMode == ComputeMode.Device ? ComputeMode.SIMD : computeMode;
    }
    
    private GpuBuffers(ComputeMode computeMode, int length, ulong hash, GpuDevice device, string firstParam) {
        this.length         = length;
        this.hash           = hash;
        this.device         = device;
        this.firstParam     = firstParam;
        this.computeMode    = computeMode == ComputeMode.Device ? device.DefaultComputeMode : computeMode;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GpuBuffers Create<T>(Buffer<T> buffer, string paramName, ComputeMode computeMode) where T : unmanaged {
        return Create(buffer.gpuBuffer, buffer.Length, paramName, computeMode);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GpuBuffers Create<T>(InBuffer<T> buffer, string paramName, ComputeMode computeMode) where T : unmanaged {
        return Create(buffer.gpuBuffer, buffer.Length, paramName, computeMode);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static GpuBuffers Create(GpuBuffer gpuBuffer, int length, string paramName, ComputeMode computeMode)
    {
        if (gpuBuffer == null) {
            if (computeMode == ComputeMode.GPU) {
                NoDevice();
            }
            return new GpuBuffers(computeMode, length);
        }
        var bufferDevice = gpuBuffer.Device;
        ulong hash;
        unchecked { hash = (OffsetBasis ^ (ulong)gpuBuffer.Id) * Prime; }
        var buffers = new GpuBuffers(computeMode, length, hash, bufferDevice, paramName);
        if (bufferDevice != null    &&
           !bufferDevice.IsDisposed)
        {
            return buffers;
        }
        buffers.ValidateError(gpuBuffer, length, paramName);
        return default;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Validate<T>(Buffer<T> buffer, string paramName) where T : unmanaged {
        Validate(buffer.gpuBuffer, buffer.Length, paramName);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Validate<T>(InBuffer<T> buffer, string paramName) where T : unmanaged {
        Validate(buffer.gpuBuffer, buffer.Length, paramName);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Validate(GpuBuffer gpuBuffer, int bufferLength, string paramName)
    {
        if (areSpans && gpuBuffer == null) {
            return;
        }
        if (gpuBuffer != null) {
            var bufferDevice = gpuBuffer.Device;
            if (bufferDevice != null    &&
               !bufferDevice.IsDisposed &&
                bufferDevice  == device  &&
                bufferLength  == length)
            {
                unchecked { hash = (hash ^ (ulong)gpuBuffer.Id) * Prime; }
                return;
            }
        }
        ValidateError(gpuBuffer, bufferLength, paramName);
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)][StackTraceHidden][DoesNotReturn]
    private void ValidateError(GpuBuffer gpuBuffer, int bufferLength, string paramName)
    {
        if ((areSpans && gpuBuffer != null) ||
           (!areSpans && gpuBuffer == null)) {
            throw new InvalidOperationException($"Identity Crisis: Parameter '{paramName}' identifies as a GPU resource but lacks hardware-credentials.");
        }
        var bufferDevice = gpuBuffer!.Device;
        if (bufferDevice == null) {
            throw new InvalidOperationException($"Existential Void: '{paramName}' is suffering from severe amnesia. It remembers being a GpuBuffer, but it has forgotten the Device that gave its life meaning. Without a Device, it’s just 8 bytes of disappointment.");
        }
        if (bufferDevice.IsDisposed) {
            throw new InvalidOperationException($"Archaeological Error: You are trying to use '{paramName}', which belongs to a Device that has already been sent to the silicon graveyard. Stop digging in the trash and use a living Device!");
        }
        if (bufferLength != length) {
            throw new InvalidOperationException($"Totalitarian Sizing: Parameter '{paramName}' (Length: {bufferLength}) is trying to start a revolution against the established order of '{firstParam}' (Length: {length}). In this method, the first parameter is the Law. Everyone else must follow its lead or be purged from the pipeline.");
        }
        if (bufferDevice == device) {
            return;    
        }
        throw new InvalidOperationException($"Diplomatic Incident: '{paramName}' is carrying a passport from a different Device-Jurisdiction. We cannot grant asylum to resources that were minted under the authority of another master. '{firstParam}' was here first; respect the borders.");
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)][StackTraceHidden][DoesNotReturn]
    private static void NoDevice() {
         throw new InvalidOperationException("The Ghost Orchestra: You've provided parameters, but not a single one carries a soul (GpuDevice). I cannot conduct a symphony of zeros. Initialize your data or go back to Scalar-Land!");
    }
}

/*
 * ======================================================================================
 * THE GHOST ORCHESTRA: A GUIDE TO THE SYMPHONY
 * ======================================================================================
 * * Status: Requires User Attention
 * Symptom: You have summoned a kernel, but the stage is empty.
 *
 * The Ghost Orchestra appears when your execution parameters are technically sound, 
 * but lack the "soul"—the GpuDevice. You are attempting to conduct a high-performance 
 * computation, but you have failed to assign a physical reality to your data.
 *
 * THE PROTOCOL FOR RESOLUTION:
 * 1. Check your ComputeMode: Are you requesting ComputeMode.GPU? If so, the system 
 * expects a live, connected GpuDevice.
 * 2. Verify the Soul: Ensure your buffer has been initialized with a valid GpuDevice 
 * via your Adapter.
 * 3. The Scalar Escape: If you do not have a GPU or simply wish to perform a quick 
 * test, revert your ComputeMode to Scalar. The "Scalar-Land" is always open and 
 * requires no hardware credentials—it is the safest harbor for your data.
 *
 * "The music of computation cannot play in the void. Provide the hardware, 
 * or embrace the simplicity of the CPU."
 * ======================================================================================
 */