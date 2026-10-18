using System.Numerics;
using Friflo.TmGui;

namespace TuiTerminal.Draw;

public class DrawDiagram
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
    private const uint SweepColor = 0xa0a0a0FF;  // vertical sweep line for current time

    public void UpdateAndDraw(TmDraw draw, float currentTime, float canvasWidth, float canvasHeight)
    {
        const float updateInterval = 1.0f / 60.0f; // 60 updates per second
        const float timeWindow = 2.0f;      // Graph spans 2 seconds across screen (120 samples at 60 Hz)

        // Push new data point onto ring buffer at 60 Hz
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

            // Clear a small clearing zone ahead of the write head to prevent connecting to old sweep data
            for (int c = 0; c < 4; c++)
            {
                int clearIdx = (head + c) % HistorySize;
                timestamps[clearIdx] = 0;
            }
        }

        float width  = canvasWidth;
        float height = canvasHeight;

        float plotTop = 20.0f;
        float plotBottom = height - 20.0f;
        float plotHeight = plotBottom - plotTop;

        // 1. Draw Static Vertical Grid Lines across the time window
        int gridDivisions = 4;
        for (int g = 1; g < gridDivisions; g++)
        {
            float gx = (g / (float)gridDivisions) * width;
            draw.StrokeLine(new Vector2(gx, plotTop), new Vector2(MathF.Floor(gx), plotBottom), 1, GridColor);
        }

        // 2. Draw Black X-Axis Line at Bottom
        draw.StrokeLine(new Vector2(0, plotBottom), new Vector2(width, plotBottom), 3, AxisColor);

        // 3. Render Cyclic Sweep Graph Lines mapped to fixed buffer indices
        for (int i = 0; i < HistorySize - 1; i++)
        {
            int idx1 = i;
            int idx2 = i + 1;

            if (timestamps[idx1] == 0 || timestamps[idx2] == 0) continue;

            float x1 = MathF.Floor(((float)idx1 / HistorySize) * width);
            float x2 = MathF.Floor(((float)idx2 / HistorySize) * width);

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

        // 4. Draw Current Time Sweep Line moving from left to right
        float sweepX = MathF.Floor(((float)head / HistorySize) * width);
        draw.StrokeLine(new Vector2(sweepX, plotTop), new Vector2(sweepX, plotBottom), 4, SweepColor);
    }
}