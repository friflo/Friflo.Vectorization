using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

// ReSharper disable InconsistentNaming
namespace Friflo.Vectorization.Intrinsics;

internal static class VectorUtils
{
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowBufferTooSmall() => throw new IndexOutOfRangeException();
}