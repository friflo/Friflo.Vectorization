// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Numerics;
using System.Runtime.CompilerServices;

// ReSharper disable UseCollectionExpression
// ReSharper disable once CheckNamespace
namespace Friflo.ImGui;

public enum ArcCorner
{
    TopLeft,     // PI -> 1.5*PI
    TopRight,    // 1.5*PI -> 2*PI
    BottomRight, // 0 -> 0.5*PI
    BottomLeft   // 0.5*PI -> PI
}

internal static class ArcLookups
{
    // Precalculated unit vectors for 1 to 10 segments over a 90-degree arc (0 to PI/2)
    public static readonly Vector2[][] CornerTables = new Vector2[][]
    {
        Array.Empty<Vector2>(),

        // 1 Segment
        new Vector2[] { new(1.00000000f, 0.00000000f), new(0.00000000f, 1.00000000f) },
        // 2 Segments
        new Vector2[] { new(1.00000000f, 0.00000000f), new(0.70710678f, 0.70710678f), new(0.00000000f, 1.00000000f) },
        // 3 Segments
        new Vector2[] { new(1.00000000f, 0.00000000f), new(0.86602540f, 0.50000000f), new(0.50000000f, 0.86602540f), new(0.00000000f, 1.00000000f) },
        // 4 Segments
        new Vector2[] { new(1.00000000f, 0.00000000f), new(0.92387953f, 0.38268343f), new(0.70710678f, 0.70710678f), new(0.38268343f, 0.92387953f), new(0.00000000f, 1.00000000f) },
        // 5 Segments
        new Vector2[] { new(1.00000000f, 0.00000000f), new(0.95105652f, 0.30901699f), new(0.80901699f, 0.58778525f), new(0.58778525f, 0.80901699f), new(0.30901699f, 0.95105652f), new(0.00000000f, 1.00000000f) },
        // 6 Segments
        new Vector2[] { new(1.00000000f, 0.00000000f), new(0.96592583f, 0.25881905f), new(0.86602540f, 0.50000000f), new(0.70710678f, 0.70710678f), new(0.50000000f, 0.86602540f), new(0.25881905f, 0.96592583f), new(0.00000000f, 1.00000000f) },
        // 7 Segments
        new Vector2[] { new(1.00000000f, 0.00000000f), new(0.97492791f, 0.22252093f), new(0.90096887f, 0.43388374f), new(0.78183148f, 0.62348980f), new(0.62348980f, 0.78183148f), new(0.43388374f, 0.90096887f), new(0.22252093f, 0.97492791f), new(0.00000000f, 1.00000000f) },
        // 8 Segments
        new Vector2[] { new(1.00000000f, 0.00000000f), new(0.98078528f, 0.19509032f), new(0.92387953f, 0.38268343f), new(0.83146961f, 0.55557023f), new(0.70710678f, 0.70710678f), new(0.55557023f, 0.83146961f), new(0.38268343f, 0.92387953f), new(0.19509032f, 0.98078528f), new(0.00000000f, 1.00000000f) },
        // 9 Segments
        new Vector2[] { new(1.00000000f, 0.00000000f), new(0.98480775f, 0.17364818f), new(0.93969262f, 0.34202014f), new(0.86602540f, 0.50000000f), new(0.76604444f, 0.64278761f), new(0.64278761f, 0.76604444f), new(0.50000000f, 0.86602540f), new(0.34202014f, 0.93969262f), new(0.17364818f, 0.98480775f), new(0.00000000f, 1.00000000f) },
        // 10 Segments
        new Vector2[] { new(1.00000000f, 0.00000000f), new(0.98768834f, 0.15643447f), new(0.95105652f, 0.30901699f), new(0.89100652f, 0.45399050f), new(0.80901699f, 0.58778525f), new(0.70710678f, 0.70710678f), new(0.58778525f, 0.80901699f), new(0.45399050f, 0.89100652f), new(0.30901699f, 0.95105652f), new(0.15643447f, 0.98768834f), new(0.00000000f, 1.00000000f) }
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetCornerTransform(ArcCorner corner, out float signX, out float signY, out bool swapXY)
    {
        switch (corner)
        {
            case ArcCorner.TopLeft:
                signX = -1f; signY = -1f; swapXY = false; break;
            case ArcCorner.TopRight:
                signX = 1f;  signY = -1f; swapXY = true;  break;
            case ArcCorner.BottomRight:
                signX = 1f;  signY = 1f;  swapXY = false; break;
            case ArcCorner.BottomLeft:
            default:
                signX = -1f; signY = 1f;  swapXY = true;  break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (float start, float end) GetCornerAngles(ArcCorner corner) => corner switch
    {
        ArcCorner.TopLeft     => (MathF.PI, MathF.PI * 1.5f),
        ArcCorner.TopRight    => (MathF.PI * 1.5f, MathF.PI * 2f),
        ArcCorner.BottomRight => (0f, MathF.PI * 0.5f),
        ArcCorner.BottomLeft  => (MathF.PI * 0.5f, MathF.PI),
        _                  => (0f, 0f)
    };
}