// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Numerics;
using System.Runtime.CompilerServices;


// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;




/*
public static class Draw2D
{
    [ThreadStatic]
    private static Batcher2D? _currentBatcher;

    public static void Begin(Batcher2D batcher) => _currentBatcher = batcher;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Rectangle(in Vector2 position, in Vector2 size, uint color)
    {
        _currentBatcher!.DrawQuad(position, size, color);
    }

    public static void End()
    {
        _currentBatcher?.Flush();
        _currentBatcher = null;
    }
} */
