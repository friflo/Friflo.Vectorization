// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;

// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU.Runtime;

public abstract class NativeTask : IDisposable
{
    public              bool                IsSubmitted     { get; set; }  // TODO protected
    public              bool                IsCompleted     { get; set; }  // TODO protected
    
    public abstract void Dispose();
}