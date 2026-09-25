using System.Numerics;
using Friflo.TmGui;

namespace TuiTerminal;

public partial class TestGuiView
{
    private void DrawDonut(TmDraw draw)
    {
        const int numMajor = 32;
        const int numMinor = 20;
        const int totalQuads = numMajor * numMinor;

        const float R = 130.0f;
        const float tubeRadius = 55.0f;

        Vector2 center = new(CanvasWidth * 0.5f, CanvasHeight * 0.5f);
        Vector3 lightDir = Vector3.Normalize(new Vector3(0.4f, -0.9f, -0.6f));

        // 3-Axis rotation (X, Y, and slow continuous Z axis roll)
        float rotX = time * 0.70f;
        float rotY = time * 1.10f;
        float rotZ = time * 0.35f; // Slow third axis

        float cosX = MathF.Cos(rotX), sinX = MathF.Sin(rotX);
        float cosY = MathF.Cos(rotY), sinY = MathF.Sin(rotY);
        float cosZ = MathF.Cos(rotZ), sinZ = MathF.Sin(rotZ);

        const uint DoughColor = 0xF8B040FF;
        const uint IcingColor = 0xFF2898FF;

        Span<Vector3> worldPoints = stackalloc Vector3[(numMajor + 1) * (numMinor + 1)];
        Span<Vector2> projected = stackalloc Vector2[(numMajor + 1) * (numMinor + 1)];

        // 1. Generate 3D grid and project to 2D screen space
        for (int i = 0; i <= numMajor; i++)
        {
            float theta = i * MathF.PI * 2.0f / numMajor;
            float cosTheta = MathF.Cos(theta), sinTheta = MathF.Sin(theta);

            for (int j = 0; j <= numMinor; j++)
            {
                float phi = j * MathF.PI * 2.0f / numMinor;
                float cosPhi = MathF.Cos(phi), sinPhi = MathF.Sin(phi);

                Vector3 pos = new((R + tubeRadius * cosPhi) * cosTheta, tubeRadius * sinPhi, (R + tubeRadius * cosPhi) * sinTheta);

                // 3D Rotation across 3 axes (Y -> X -> Z)
                float x1 = pos.X * cosY + pos.Z * sinY;
                float z1 = -pos.X * sinY + pos.Z * cosY;
                
                float y2 = pos.Y * cosX - z1 * sinX;
                float z2 = pos.Y * sinX + z1 * cosX;

                float x3 = x1 * cosZ - y2 * sinZ;
                float y3 = x1 * sinZ + y2 * cosZ;

                int idx = i * (numMinor + 1) + j;
                worldPoints[idx] = new Vector3(x3, y3, z2);

                float perspective = 600.0f / (600.0f + z2);
                projected[idx] = new Vector2(center.X + x3 * perspective, center.Y + y3 * perspective);
            }
        }

        // 2. Build quad list for Painter's Algorithm with precise depth
        Span<(int i, int j, float depth)> quadList = stackalloc (int, int, float)[totalQuads];
        int quadCount = 0;

        for (int i = 0; i < numMajor; i++)
        {
            for (int j = 0; j < numMinor; j++)
            {
                int idx00 = i * (numMinor + 1) + j;
                int idx10 = (i + 1) * (numMinor + 1) + j;
                int idx11 = (i + 1) * (numMinor + 1) + (j + 1);
                int idx01 = i * (numMinor + 1) + (j + 1);

                float avgZ = (worldPoints[idx00].Z + worldPoints[idx10].Z + worldPoints[idx11].Z + worldPoints[idx01].Z) * 0.25f;

                quadList[quadCount++] = (i, j, avgZ);
            }
        }

        // Sort back-to-front (largest Z first)
        for (int i = 0; i < quadCount - 1; i++)
        {
            for (int k = i + 1; k < quadCount; k++)
            {
                if (quadList[i].depth < quadList[k].depth)
                {
                    var temp = quadList[i];
                    quadList[i] = quadList[k];
                    quadList[k] = temp;
                }
            }
        }

        // 3. Render Quads
        foreach (var (i, j, _) in quadList)
        {
            int idx00 = i * (numMinor + 1) + j;
            int idx10 = (i + 1) * (numMinor + 1) + j;
            int idx11 = (i + 1) * (numMinor + 1) + (j + 1);
            int idx01 = i * (numMinor + 1) + (j + 1);

            Vector3 w0 = worldPoints[idx00];
            Vector3 w1 = worldPoints[idx10];
            Vector3 w2 = worldPoints[idx11];

            Vector2 p0 = projected[idx00];
            Vector2 p1 = projected[idx10];
            Vector2 p2 = projected[idx11];
            Vector2 p3 = projected[idx01];

            // Backface Culling in 2D
            float cross = (p1.X - p0.X) * (p2.Y - p0.Y) - (p1.Y - p0.Y) * (p2.X - p0.X);
            if (cross <= 0) continue;

            // Calculate true face normal in 3D for Flat Shading
            Vector3 edge1 = w1 - w0;
            Vector3 edge2 = w2 - w0;
            Vector3 faceNormal = Vector3.Normalize(Vector3.Cross(edge1, edge2));

            // Dynamic light contrast
            float dot = Vector3.Dot(faceNormal, -lightDir);
            float intensity = Math.Clamp(0.25f + 0.75f * MathF.Max(0.0f, dot), 0.25f, 1.0f);

            // Icing mask on top outer surface
            float phi = j * MathF.PI * 2.0f / numMinor;
            bool isIcing = phi >= MathF.PI * 0.10f && phi <= MathF.PI * 0.70f;
            uint baseColor = isIcing ? IcingColor : DoughColor;

            byte red   = (byte)(((baseColor >> 24) & 0xFF) * intensity);
            byte green = (byte)(((baseColor >> 16) & 0xFF) * intensity);
            byte blue  = (byte)(((baseColor >> 8)  & 0xFF) * intensity);
            uint fillColor = (uint)((red << 24) | (green << 16) | (blue << 8) | 0xFF);

            // Fill quad
            draw.FillTriangle(p0, p1, p2, fillColor);
            draw.FillTriangle(p0, p2, p3, fillColor);

            // 4. Render randomly rotated sprinkles on icing quads
            uint seed = (uint)(i * 7919 + j * 65537);
            bool hasSprinkle = isIcing && ((seed % 100) < 35);

            if (hasSprinkle)
            {
                // Pseudo-random position offset inside the quad
                float offsetX = ((seed & 0xFF) / 255.0f - 0.5f) * 0.5f;
                float offsetY = (((seed >> 8) & 0xFF) / 255.0f - 0.5f) * 0.5f;

                Vector2 quadCenter = (p0 + p1 + p2 + p3) * 0.25f;
                Vector2 uDir = (p1 - p0) * offsetX;
                Vector2 vDir = (p3 - p0) * offsetY;
                Vector2 sprPos = quadCenter + uDir + vDir;

                // Fully randomized rotation angle per sprinkle
                float sprAngle = ((seed ^ (seed >> 7)) & 0xFFFF) * (MathF.PI * 2.0f / 65535.0f);
                Vector2 dir = new(MathF.Cos(sprAngle), MathF.Sin(sprAngle));
                Vector2 perp = new(-dir.Y, dir.X);

                float sprLen = 3.8f;
                float sprThick = 1.3f;

                uint sprColor = GetVibrantSprinkleColor(seed);

                Vector2 sp0 = sprPos - dir * sprLen - perp * sprThick;
                Vector2 sp1 = sprPos + dir * sprLen - perp * sprThick;
                Vector2 sp2 = sprPos + dir * sprLen + perp * sprThick;
                Vector2 sp3 = sprPos - dir * sprLen + perp * sprThick;

                draw.FillTriangle(sp0, sp1, sp2, sprColor);
                draw.FillTriangle(sp0, sp2, sp3, sprColor);
            }
        }
    }

    private static uint GetVibrantSprinkleColor(uint seed)
    {
        uint h = seed ^ (seed >> 13);
        h *= 0x5bd1e995;
        h ^= h >> 15;

        return (h % 6) switch
        {
            0 => 0x00FFFFFF, // Cyan
            1 => 0xFFFF00FF, // Yellow
            2 => 0x00FF33FF, // Green
            3 => 0xFF3300FF, // Orange-Red
            4 => 0xCC33FFFF, // Purple
            _ => 0xFFFFFFFF  // White
        };
    }
}