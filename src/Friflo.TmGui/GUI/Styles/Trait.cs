// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;

// ReSharper disable once CheckNamespace
namespace Friflo.SmGui;

[Flags]
public enum SmTrait
{
    Border = 1 << 0,
    Shadow = 2 << 0,
}

public static class SmTraitExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Has(this SmTrait traits, SmTrait trait) => (traits & trait) != 0;
}