using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

// ReSharper disable InconsistentNaming
namespace Generators.Static;

internal static class VectorUtils
{
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowBufferTooSmall() => throw new IndexOutOfRangeException();
}