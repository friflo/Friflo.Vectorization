namespace TuiTerminal;


enum DrawType
{
    None,
    Primitives,
    Cubes,
    Donut,
    LiveDiagram,
}

public class AppState
{
    internal    bool        rotateTexture;
    internal    DrawType    drawType = DrawType.Donut;
    internal    bool        useTerminalPixels;
    internal    bool        textureScissor;
    internal    float       speed = 0.1f;
    
    internal readonly List<string>  scrollAreaButtons = [];
}