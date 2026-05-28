using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

// ReSharper disable InconsistentNaming
namespace Generators.Static;

internal static class VectorUtils
{
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowBufferTooSmall(string paramName)
    {
        throw new IndexOutOfRangeException($"Buffer '{paramName}' is too small for SIMD alignment.");
    }
}