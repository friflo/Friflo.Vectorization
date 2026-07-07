// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;

// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU;

public sealed class GpuException : Exception
{
    public readonly ErrorType errorType;
    
    internal GpuException (ErrorType errorType, string message) : base(message) {
        this.errorType = errorType;
    }
}