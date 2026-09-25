using System.Numerics;
using Friflo.TmGui;

namespace TuiTerminal;

public class LiveDiagram
{
    private const int HistorySize = 120;
    private readonly float[] redValues = new float[HistorySize];
    private readonly float[] orangeValues = new float[HistorySize];
    private readonly float[] violetValues = new float[HistorySize];
    private readonly float[] timestamps = new float[HistorySize];

    private int head;
    private readonly Random random = new();
    private float lastUpdateTime;

    // Colors (RGBA packed)
    private const uint RedColor = 0xFF2222FF;
    private const uint OrangeColor = 0xFF8C00FF;
    private const uint VioletColor = 0xAA33FFFF;
    private const uint AxisColor = 0x000000FF;   // Black X-Axis
    private const uint GridColor = 0xffffffFF;

    internal void UpdateAndDraw(TmDraw draw, float currentTime)
    {
        const float updateInterval = 0.05f; // 20 updates per second
        const float timeWindow = 6.0f;      // Graph spans 6 seconds across screen
        const float scrollSpeed = TestGuiView.CanvasWidth / timeWindow;

        // Push new data point onto ring buffer
        if (currentTime - lastUpdateTime >= updateInterval)
        {
            lastUpdateTime = currentTime;

            int prev = (head - 1 + HistorySize) % HistorySize;
            float prevRed = redValues[prev] == 0 ? 0.5f : redValues[prev];
            float prevOrange = orangeValues[prev] == 0 ? 0.5f : orangeValues[prev];
            float prevViolet = violetValues[prev] == 0 ? 0.5f : violetValues[prev];

            timestamps[head] = currentTime;
            redValues[head] = Math.Clamp(prevRed + ((float)random.NextDouble() - 0.5f) * 0.12f, 0.05f, 0.95f);
            orangeValues[head] = Math.Clamp(prevOrange + ((float)random.NextDouble() - 0.5f) * 0.12f, 0.05f, 0.95f);
            violetValues[head] = Math.Clamp(prevViolet + ((float)random.NextDouble() - 0.5f) * 0.12f, 0.05f, 0.95f);

            head = (head + 1) % HistorySize;
        }

        float width = TestGuiView.CanvasWidth;
        float height = TestGuiView.CanvasHeight;

        float plotTop = 20.0f;
        float plotBottom = height - 20.0f;
        float plotHeight = plotBottom - plotTop;

        // 1. Draw Darker Gray Y-Axis Grid Lines (3 pixels wide) Scrolling Synchronously with Time
        float gridSpacing = 120.0f;
        float scrollOffset = (currentTime * scrollSpeed) % gridSpacing;

        for (float x = width - scrollOffset; x >= 0; x -= gridSpacing)
        {
            draw.StrokeLine(new Vector2(x, plotTop), new Vector2(MathF.Floor(x), plotBottom), 4, GridColor);
        }

        // 2. Draw Black X-Axis Line at Bottom
        draw.StrokeLine(new Vector2(0, plotBottom), new Vector2(width, plotBottom), 3, AxisColor);

        // 3. Render Graph Lines Continuously Gliding using 'currentTime'
        for (int i = 0; i < HistorySize - 1; i++)
        {
            int idx1 = (head - HistorySize + i + HistorySize) % HistorySize;
            int idx2 = (head - HistorySize + i + 1 + HistorySize) % HistorySize;

            if (timestamps[idx1] == 0 || timestamps[idx2] == 0) continue;

            float x1 = MathF.Floor(width - (currentTime - timestamps[idx1]) * scrollSpeed);
            float x2 = MathF.Floor(width - (currentTime - timestamps[idx2]) * scrollSpeed);

            if (x2 < 0 || x1 > width) continue;

            // Red Line
            float yRed1 = plotBottom - (redValues[idx1] * plotHeight);
            float yRed2 = plotBottom - (redValues[idx2] * plotHeight);
            draw.StrokeLine(new Vector2(x1, yRed1), new Vector2(x2, yRed2), 2, RedColor);

            // Orange Line
            float yOrange1 = plotBottom - (orangeValues[idx1] * plotHeight);
            float yOrange2 = plotBottom - (orangeValues[idx2] * plotHeight);
            draw.StrokeLine(new Vector2(x1, yOrange1), new Vector2(x2, yOrange2), 2, OrangeColor);

            // Violet Line
            float yViolet1 = plotBottom - (violetValues[idx1] * plotHeight);
            float yViolet2 = plotBottom - (violetValues[idx2] * plotHeight);
            draw.StrokeLine(new Vector2(x1, yViolet1), new Vector2(x2, yViolet2), 2, VioletColor);
        }
    }
}