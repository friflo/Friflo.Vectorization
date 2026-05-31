// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU.Runtime;

// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU;


public sealed partial class WgpuDevice
{
    public CommandRecorder Recorder
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)] [StackTraceHidden]
        get {
            var context = Context;
            if (context == null) throw MissingContextException();
            ValidateThreadSafety(context);
            return (CommandRecorder)context;
        }
    }
    
    private InvalidOperationException MissingContextException() {
        return new InvalidOperationException($"Missing Context on device: '{Label}'. Call device.BeginContext() first.");
    }

    protected override PipelineContext NewPipelineContext() => new CommandRecorder(this);
}