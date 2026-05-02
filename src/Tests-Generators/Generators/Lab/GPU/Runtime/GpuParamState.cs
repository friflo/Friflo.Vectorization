// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;

namespace Friflo.Vectorization.GPU.Runtime;

[EditorBrowsable(EditorBrowsableState.Never)]
public struct GpuParamState
{
    private GpuDevice   device;
    private string      firstParam;

    public void Validate(Buffer<float> buffer, string paramName)
    {
        var gpuBuffer = buffer.gpuBuffer;
        if (gpuBuffer == null) {
            throw new InvalidOperationException($"Identity Crisis: Parameter '{paramName}' identifies as a GPU resource but lacks the hardware-credentials. Stop pretending and provide a real GpuBuffer!");
        }
        var bufferDevice = gpuBuffer.Device;
        if (bufferDevice == null) {
            throw new InvalidOperationException($"Orphaned Buffer: '{paramName}' GpuBuffer<> already disposed.");
        }
        if (bufferDevice.IsDisposed) {
            throw new InvalidOperationException($"Architectural Blasphemy: Parameter '{paramName}' belongs to a Device that has already been destroyed.");
        }
        if (bufferDevice == device) {
            return;    
        }
        if (device == null) {
            firstParam  = paramName;
            device      = bufferDevice;
            return;
        }
        throw new InvalidOperationException($"Contextual Polygamy: Parameter '{paramName}' is trying to cheat on Context with a different master. It doesn't match the Context established by '{firstParam}'. In this library, we practice Monogamy.");
    }

    public GpuDevice GetDevice() {
        if (device != null) {
            return device;
        }
        throw new InvalidOperationException("The Ghost Orchestra: You've provided parameters, but not a single one carries a soul (GpuDevice). I cannot conduct a symphony of zeros. Initialize your data or go back to Scalar-Land!");
    }
}
