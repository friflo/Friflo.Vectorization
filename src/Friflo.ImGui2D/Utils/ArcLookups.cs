// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Numerics;
using System.Runtime.CompilerServices;

// ReSharper disable InconsistentNaming
// ReSharper disable UseCollectionExpression
// ReSharper disable once CheckNamespace
namespace Friflo.ImGui2D;

public enum ArcCorner
{
    TopLeft,     // PI      -> 1.5*PI
    TopRight,    // 1.5*PI  -> 2*PI
    BottomRight, // 0       -> 0.5*PI
    BottomLeft   // 0.5*PI  -> PI
}

internal static class ArcLookups
{
    internal const int CornerTableLength = 32;
    
    // Precalculated unit vectors segments over a 90-degree arc (0 to PI/2)
    internal static readonly Vector2[][] CornerTables = BuildCornerTables(CornerTableLength);

    private static Vector2[][] BuildCornerTables(int maxSegments)
    {
        Vector2[][] tables = new Vector2[maxSegments + 1][];
        tables[0] = Array.Empty<Vector2>();

        for (int segs = 1; segs <= maxSegments; segs++)
        {
            Vector2[] arc = new Vector2[segs + 1];
            float step = (MathF.PI * 0.5f) / segs;

            for (int i = 0; i <= segs; i++) {
                float angle = i * step;
                arc[i] = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            }
            tables[segs] = arc;
        }
        return tables;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void GetCornerTransform(ArcCorner corner, out float signX, out float signY, out bool swapXY)
    {
        switch (corner)
        {
            case ArcCorner.TopLeft:     signX = -1f; signY = -1f; swapXY = false; break;
            case ArcCorner.TopRight:    signX = 1f;  signY = -1f; swapXY = true;  break;
            case ArcCorner.BottomRight: signX = 1f;  signY = 1f;  swapXY = false; break;
            default:
            case ArcCorner.BottomLeft:  signX = -1f; signY = 1f;  swapXY = true;  break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static (float start, float end) GetCornerAngles(ArcCorner corner) => corner switch
    {
        ArcCorner.TopLeft       => (MathF.PI, MathF.PI * 1.5f),
        ArcCorner.TopRight      => (MathF.PI * 1.5f, MathF.PI * 2f),
        ArcCorner.BottomRight   => (0f, MathF.PI * 0.5f),
        ArcCorner.BottomLeft    => (MathF.PI * 0.5f, MathF.PI),
        _                       => (0f, 0f)
    };
}