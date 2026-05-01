// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;

namespace Friflo.Vectorization.GPU.Runtime;

[EditorBrowsable(EditorBrowsableState.Never)]
public struct GpuParamState
{
    public GpuContext   context;
    public string       firstParam;

    public unsafe void Validate(Buffer<float> buffer, string paramName)
    {
        var gpuBuffer = buffer.gpuBuffer;
        if (gpuBuffer == null) {
            throw new InvalidOperationException($"Identity Crisis: Parameter '{paramName}' identifies as a GPU resource but lacks the hardware-credentials. Stop pretending and provide a real GpuBuffer!");
        }
        if (gpuBuffer.handle != null)
        {
            if (gpuBuffer.context == context) {
                return;    
            }
            if (context == null) {
                firstParam   = paramName;
                context      = gpuBuffer.context;
                return;
            }
            throw new InvalidOperationException($"Contextual Polygamy: Parameter '{paramName}' is trying to cheat on Context with a different master. It doesn't match the Context established by '{firstParam}'. In this library, we practice Monogamy.");
        }
        throw new InvalidOperationException(
            $"Architectural Blasphemy: You are trying to extract the Context from parameter '{paramName}', which you've already sent to the void. A disposed Buffer has no God and no GPU memory.");
    }
    
    public GpuContext GetContext() {
        if (context != null) {
            return context;
        }
        throw new InvalidOperationException("The Ghost Orchestra: You've provided parameters, but not a single one carries a soul (GpuContext). I cannot conduct a symphony of zeros. Initialize your data or go back to Scalar-Land!");
    }
}
