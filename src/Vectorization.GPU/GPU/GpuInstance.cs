// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;

// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU;

public abstract class GpuInstance : IDisposable
{
    public abstract bool            IsDisposed { get;  }
    
    public abstract void            Dispose();
}