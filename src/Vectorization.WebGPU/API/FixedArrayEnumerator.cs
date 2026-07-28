// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Runtime.CompilerServices;

// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU;

    public ref struct FixedArrayEnumerator<T>  where T : unmanaged
    {
        private             int     offset;     //  4
        private readonly    int     stride;     //  4
        private readonly    int     size;       //  4
        private readonly    ref T   element0;
        
        public FixedArrayEnumerator(ref T element0, int stride, int size) {
            offset          = -stride;
            this.stride     =  stride;
            this.size       =  size;
            this.element0   =  ref element0;
        }
        
        public ref T Current => ref Unsafe.AddByteOffset(ref element0, offset < 0 ? 0 : offset);
        
        public bool MoveNext() {
            int nextOffset = offset + stride;
            if (nextOffset < size) {
                offset = nextOffset;
                return true;
            }
            return false;
        }

        public void Reset() {
            offset = -stride;
        }
    }