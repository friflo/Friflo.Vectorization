namespace TuiTerminal;


enum DrawType
{
    Primitives,
    Cubes,
    Donut
}

public class AppState
{
    internal    bool        rotateTexture;
    internal    DrawType    drawType;
    internal    bool        useTerminalPixels;
    internal    bool        textureScissor;
    internal    float       speed = 0.1f;
    
    internal readonly List<string>  scrollAreaButtons = [];
}