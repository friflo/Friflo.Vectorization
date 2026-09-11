// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;

// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;

[Flags]
public enum TmTrait
{
    Border = 1 << 0
}

public static class TmTraitExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Has(this TmTrait traits, TmTrait trait) => (traits & trait) != 0;
}