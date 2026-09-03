// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Headless;


public sealed class MemoryBuffer<T> :IDisposable where T : unmanaged
{
    public readonly Memory<T> memory;
    
    internal MemoryBuffer(int size) {
        memory = new Memory<T>(new T[size]);
    }

    public void Dispose() { }
}

internal sealed class HeadlessBuffer<T> : TmBuffer<T> where T : unmanaged
{
    private readonly  MemoryBuffer<T> native;
    
    public   override Memory<T>     Memory => native.memory;
    
    public HeadlessBuffer(MemoryBuffer<T> buffer) {
        native = buffer;
    }

    public override void Dispose() {
        native.Dispose();
    }
    
    public override void Write(int start, int length) {
        // <copy buffer -> GPU>
    }
}