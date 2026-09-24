using System.Numerics;
using Friflo.TmGui;

namespace TuiTerminal;

public partial class TestGuiView
{
    private readonly (int i0, int i1, int i2, int i3, Vector3 norm)[] faces = [
        (0, 1, 2, 3, new(0, 0, -1)), // Front
        (5, 4, 7, 6, new(0, 0,  1)), // Back
        (4, 0, 3, 7, new(-1, 0, 0)), // Left
        (1, 5, 6, 2, new(1, 0, 0)),  // Right
        (4, 5, 1, 0, new(0, -1, 0)), // Top
        (3, 2, 6, 7, new(0, 1, 0))   // Bottom
    ];

    private readonly uint[] cubePalette = [
        0x00f0ff00, 0xff008800, 0xffaa0000, 0x00ff6600,
        0xaa00ff00, 0xffe60000, 0xff000000, 0x0088ff00,
        0x00ffcc00, 0xff00aa00, 0xd4ff0000, 0xff550000,
        0x7700ff00, 0x00fff000, 0xff007700, 0xffb70000
    ];
        
    private void DrawCubes(TmDraw draw)
    {
        // Canvas-Hintergrund
        // draw.FillRect(new Vector2(0, 0), new Vector2(CanvasWidth, CanvasHeight), Color32.Black);

        ReadOnlySpan<Vector3> vertices = [
            new(-1, -1, -1), new( 1, -1, -1), new( 1,  1, -1), new(-1,  1, -1),
            new(-1, -1,  1), new( 1, -1,  1), new( 1,  1,  1), new(-1,  1,  1)
        ];

        Vector3 lightDir = Vector3.Normalize(new Vector3(0.2f, -1.0f, -0.5f));
        Vector2 center = new(CanvasWidth * 0.5f, CanvasHeight * 0.5f);

        const int cubeCount = 16;
        
        // Radien in Bildschirmpixeln abgestimmt auf Canvas (800x500)
        float radiusX = CanvasWidth  * 0.40f; // Nutzt die Breite aus
        float radiusY = CanvasHeight * 0.22f; // Erzeugt die Draufsicht/Kippung (~30°-40° Look)
        float cubeSize = 32.0f;               // Angemessene Würfelgröße

        // Z-Sorting Buffer (Painter's Algorithm)
        Span<(int index, float depth)> cubeDepths = stackalloc (int, float)[cubeCount];

        for (int i = 0; i < cubeCount; i++)
        {
            float ringAngle = time * 0.5f + (i * MathF.PI * 2.0f / cubeCount);
            // Tiefe ergibt sich aus der Y-Position auf der Ellipse
            float depth = MathF.Cos(ringAngle); 
            cubeDepths[i] = (i, depth);
        }

        // Von hinten nach vorne sortieren
        for (int i = 0; i < cubeCount - 1; i++)
        {
            for (int j = i + 1; j < cubeCount; j++)
            {
                if (cubeDepths[i].depth > cubeDepths[j].depth)
                {
                    var temp = cubeDepths[i];
                    cubeDepths[i] = cubeDepths[j];
                    cubeDepths[j] = temp;
                }
            }
        }
        Span<Vector2> projected = stackalloc Vector2[8];
        
        // 16 Würfel rendern
        foreach (var (cubeIdx, _) in cubeDepths)
        {
            float ringAngle = time * 0.5f + (cubeIdx * MathF.PI * 2.0f / cubeCount);

            // Position des Würfels im Ring (Gekippte Kreisbahn)
            Vector2 cubeCenter = new(
                center.X + MathF.Sin(ringAngle) * radiusX,
                center.Y + MathF.Cos(ringAngle) * radiusY
            );

            // Perspektivische Skalierung: Vorderseite etwas größer als Hinterseite
            float scaleFactor = 0.82f + (MathF.Cos(ringAngle) + 1.0f) * 0.22f;
            float currentScale = cubeSize * scaleFactor;

            // Eigendrehung des Würfels
            float rotX = time * 1.0f + cubeIdx;
            float rotY = time * 1.4f + cubeIdx;

            float cosX = MathF.Cos(rotX), sinX = MathF.Sin(rotX);
            float cosY = MathF.Cos(rotY), sinY = MathF.Sin(rotY);

            uint baseColor = cubePalette[cubeIdx % cubePalette.Length];

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];

                // 3D-Eigenrotation
                float x1 = v.X * cosY + v.Z * sinY;
                float z1 = -v.X * sinY + v.Z * cosY;
                float y2 = v.Y * cosX - z1 * sinX;
                float z2 = v.Y * sinX + z1 * cosX;

                // Direkt in 2D Screen-Space projizieren
                projected[i] = new Vector2(
                    cubeCenter.X + x1 * currentScale,
                    cubeCenter.Y + y2 * currentScale
                );
            }

            foreach (var face in faces)
            {
                Vector2 p0 = projected[face.i0];
                Vector2 p1 = projected[face.i1];
                Vector2 p2 = projected[face.i2];
                Vector2 p3 = projected[face.i3];

                // Backface Culling
                float cross = (p1.X - p0.X) * (p2.Y - p0.Y) - (p1.Y - p0.Y) * (p2.X - p0.X);
                if (cross <= 0) continue;

                // Normale rotieren
                Vector3 n = face.norm;
                float nx = n.X * cosY + n.Z * sinY;
                float nz = -n.X * sinY + n.Z * cosY;
                float ny = n.Y * cosX - nz * sinX;

                // Hohe Grundhelligkeit (Ambient 0.5f)
                float intensity = MathF.Max(0.50f, Vector3.Dot(new Vector3(nx, ny, nz), -lightDir));

                byte r = (byte)(((baseColor >> 24) & 0xFF) * intensity);
                byte g = (byte)(((baseColor >> 16) & 0xFF) * intensity);
                byte b = (byte)(((baseColor >> 8)  & 0xFF) * intensity);

                uint fillColor = (uint)((r << 24) | (g << 16) | (b << 8) | 0xFF);

                draw.FillTriangle(p0, p1, p2, fillColor);
                draw.FillTriangle(p0, p2, p3, fillColor);

                // Saubere, knackige weiße Kontur
                draw.StrokeLine(p0, p1, 1, 0xffffffff);
                draw.StrokeLine(p1, p2, 1, 0xffffffff);
                draw.StrokeLine(p2, p3, 1, 0xffffffff);
                draw.StrokeLine(p3, p0, 1, 0xffffffff);
            }
        }
    }
}