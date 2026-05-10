// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;

// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU._Native;

[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class NativeTask : IDisposable
{
    public  bool    IsSubmitted     { get; protected set; }
    public  bool    IsCompleted     { get; protected set; }
    
    public abstract void Dispose();
}