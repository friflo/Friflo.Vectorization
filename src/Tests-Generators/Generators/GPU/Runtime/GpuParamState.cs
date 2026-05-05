// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

// ReSharper disable MergeIntoPattern
namespace Friflo.Vectorization.GPU.Runtime;

[EditorBrowsable(EditorBrowsableState.Never)]
public struct GpuParamState
{
    private GpuDevice   device;
    private string      firstParam;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Validate(Buffer<float> buffer, string paramName)
    {
        var gpuBuffer = buffer.gpuBuffer;
        if (gpuBuffer != null) {
            var bufferDevice = gpuBuffer.Device;
            if (bufferDevice != null && !bufferDevice.IsDisposed && bufferDevice == device) {
                return;
            }
            if (device == null) {
                firstParam  = paramName;
                device      = bufferDevice;
                return;
            }
        }
        ValidateError(buffer, paramName);
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)][StackTraceHidden][DoesNotReturn]
    private void ValidateError(Buffer<float> buffer, string paramName)
    {
        var gpuBuffer = buffer.gpuBuffer;
        if (gpuBuffer == null) {
            throw new InvalidOperationException($"Identity Crisis: Parameter '{paramName}' identifies as a GPU resource but lacks hardware-credentials.");
        }
        var bufferDevice = gpuBuffer.Device;
        if (bufferDevice == null) {
            throw new InvalidOperationException($"Existential Void: '{paramName}' is suffering from severe amnesia. It remembers being a GpuBuffer, but it has forgotten the Device that gave its life meaning. Without a Device, it’s just 8 bytes of disappointment.");
        }
        if (bufferDevice.IsDisposed) {
            throw new InvalidOperationException($"Archaeological Error: You are trying to use '{paramName}', which belongs to a Device that has already been sent to the silicon graveyard. Stop digging in the trash and use a living Device!");
        }
        if (bufferDevice == device) {
            return;    
        }
        if (device == null) {
            firstParam  = paramName;
            device      = bufferDevice;
            return;
        }
        throw new InvalidOperationException($"Diplomatic Incident: '{paramName}' is carrying a passport from a different Device-Jurisdiction. We cannot grant asylum to resources that were minted under the authority of another master. '{firstParam}' was here first; respect the borders.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public GpuDevice GetDevice() {
        if (device != null) {
            return device;
        }
        throw NoDevice();
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)][StackTraceHidden][DoesNotReturn]
    private static InvalidOperationException NoDevice() {
         return new InvalidOperationException("The Ghost Orchestra: You've provided parameters, but not a single one carries a soul (GpuDevice). I cannot conduct a symphony of zeros. Initialize your data or go back to Scalar-Land!");
    }
}
