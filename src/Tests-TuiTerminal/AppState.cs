using Friflo.SmGui;

namespace TuiTerminal;


public enum DrawType
{
    None,
    Primitives,
    Cubes,
    Donut,
    LiveDiagram,
}

/// <summary> Single <see cref="AppState"/> instance is shared by all sessions. </summary>
public class AppState
{
    internal            bool        rotateTexture;
    internal            DrawType    drawType = DrawType.None;
    internal            bool        useTerminalPixels;
    internal            bool        textureScissor;
    internal            float       speed = 0.1f;
    internal readonly   SmTexture   worldTileset;   // texture shared by all sessions
    
    internal readonly List<string>  scrollAreaButtons = [];
    
    public AppState(SmGuiBackend backend)
    {
        using var stream    = typeof(AppState).Assembly.GetManifestResourceStream("TuiTerminal.Assets.world_tileset.png")!;
        worldTileset        = backend.LoadTexture(stream, "world_tileset.png");
    }
}