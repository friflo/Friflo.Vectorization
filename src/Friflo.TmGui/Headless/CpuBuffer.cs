// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;

// ReSharper disable ConvertToAutoProperty
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Headless;



internal sealed class CpuMemoryBuffer<T> : TmBuffer<T> where T : unmanaged
{
    private readonly  Memory<T>     native;
    
    public   override Memory<T>     Memory => native;
    
    internal CpuMemoryBuffer() { }
    
    internal CpuMemoryBuffer(int length) {
        native = new Memory<T>(new T[length]);
    }

    public override void Dispose() {
    }
    
    public override void Write(int start, int length) {
        // <copy buffer -> GPU>
    }
}