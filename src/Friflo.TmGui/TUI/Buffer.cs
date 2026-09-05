// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;

// ReSharper disable ConvertToPrimaryConstructor
namespace Friflo.TmGui.TUI;


internal sealed class TuiBuffer<T> : TmBuffer<T> where T : unmanaged
{
    
    public   override Memory<T>     Memory => default;
    
    public override void Dispose() {
    }
    
    public override void Write(int start, int length) {
        // <copy buffer -> GPU>
    }
}