using System.Numerics;
using Friflo.SmGui;

namespace TuiTerminal.Draw;

public class DrawDiagram
{
    private const int HistorySize = 120;
    private readonly float[] redValues = new float[HistorySize];
    private readonly float[] orangeValues = new float[HistorySize];
    private readonly float[] violetValues = new float[HistorySize];
    private readonly float[] timestamps = new float[HistorySize];

    private int head;
    private float lastUpdateTime;

    // Colors (RGBA packed)
    private const uint RedColor = 0xFF2222FF;
    private const uint OrangeColor = 0xFF8C00FF;
    private const uint VioletColor = 0xAA33FFFF;
    private const uint AxisColor = 0x000000FF;   // Black X-Axis
    private const uint GridColor = 0xffffffFF;

    // Updates the data buffer at 60 Hz with smooth wave variations
    public void Update(float currentTime)
    {
        const float updateInterval = 1.0f / 60.0f; // 60 updates per second
        const float timeWindow = 2.0f;

        // Prevent massive catch-up loops if the app was paused for a long time
        if (currentTime - lastUpdateTime > timeWindow)
        {
            lastUpdateTime = currentTime - timeWindow;
        }

        // Catch up on missed updates if not called permanently
        while (currentTime - lastUpdateTime >= updateInterval)
        {
            lastUpdateTime += updateInterval;

            // Generate dynamic, multi-frequency smooth curves for more variations
            float t = lastUpdateTime;
            float red = 0.5f + 0.22f * MathF.Sin(t * 3.5f) + 0.15f * MathF.Cos(t * 7.1f) + 0.08f * MathF.Sin(t * 12.4f);
            float orange = 0.5f + 0.22f * MathF.Cos(t * 2.8f) + 0.15f * MathF.Sin(t * 6.3f) + 0.08f * MathF.Cos(t * 10.1f);
            float violet = 0.5f + 0.22f * MathF.Sin(t * 4.1f + 1.0f) + 0.15f * MathF.Cos(t * 8.5f) + 0.08f * MathF.Sin(t * 14.2f);

            timestamps[head] = lastUpdateTime;
            redValues[head] = Math.Clamp(red, 0.05f, 0.95f);
            orangeValues[head] = Math.Clamp(orange, 0.05f, 0.95f);
            violetValues[head] = Math.Clamp(violet, 0.05f, 0.95f);

            head = (head + 1) % HistorySize;

            // Clear a small clearing zone ahead of the write head to prevent connecting to old sweep data
            for (int c = 0; c < 6; c++)
            {
                int clearIdx = (head + c) % HistorySize;
                timestamps[clearIdx] = 0;
            }
        }
    }

    // Renders the chart and smooth marker circles without lingering old data artifacts
    public void Draw(SmDraw draw, float currentTime, float canvasWidth, float canvasHeight)
    {
        const float timeWindow = 2.0f;      // Graph spans 2 seconds across screen

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

        // 3. Render Cyclic Sweep Graph Lines (only valid, non-stale points within current time window)
        for (int i = 0; i < HistorySize - 1; i++)
        {
            int idx1 = i;
            int idx2 = i + 1;

            if (timestamps[idx1] == 0 || timestamps[idx2] == 0) continue;

            // Ensure points belong to the current active time sweep window and are contiguous
            if (currentTime - timestamps[idx1] > timeWindow || currentTime - timestamps[idx2] > timeWindow) continue;
            if (timestamps[idx1] > currentTime || timestamps[idx2] > currentTime) continue;
            if (MathF.Abs(timestamps[idx2] - timestamps[idx1]) > 0.1f) continue;

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

        // 4. Draw smooth marker circles (radius 5.0f) at the exact smooth frame-time position
        float sweepProgress = (currentTime % timeWindow) / timeWindow;
        float sweepX = MathF.Floor(sweepProgress * width);
        int currentIdx = (head - 1 + HistorySize) % HistorySize;

        if (timestamps[currentIdx] != 0 && (currentTime - timestamps[currentIdx] <= timeWindow))
        {
            float yRed = plotBottom - (redValues[currentIdx] * plotHeight);
            float yOrange = plotBottom - (orangeValues[currentIdx] * plotHeight);
            float yViolet = plotBottom - (violetValues[currentIdx] * plotHeight);

            draw.FillCircle(new Vector2(sweepX, yRed), 5.0f, RedColor);
            draw.FillCircle(new Vector2(sweepX, yOrange), 5.0f, OrangeColor);
            draw.FillCircle(new Vector2(sweepX, yViolet), 5.0f, VioletColor);
        }
    }
}