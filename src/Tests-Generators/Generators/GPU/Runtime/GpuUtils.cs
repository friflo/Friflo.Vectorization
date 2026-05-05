// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Text;

namespace Friflo.Vectorization.GPU.Runtime;


internal static class GpuUtils
{
    internal static int GetMaxCount(ReadOnlySpan<char> span)
    {
        return span.IsEmpty ? 1 : Encoding.UTF8.GetMaxByteCount(span.Length) + 1; // + \0
    }
    
    internal static unsafe void CopySpanToBuffer(ReadOnlySpan<char> span, byte* destBuffer, int destLength)
    {
        if (span.IsEmpty) {
            destBuffer[0] = 0;
            return;
        }
        var dest = new Span<byte>(destBuffer, destLength);
        int actualByteCount = Encoding.UTF8.GetBytes(span, dest);
        destBuffer[actualByteCount] = 0; // Null-terminator
    }
}
