namespace TuiTerminal;

public class AppState
{
    internal    bool    rotateTexture;
    internal    bool    showCubes;
    internal    bool    useTerminalPixels;
    internal    bool    textureScissor;
    internal    float   speed = 0.1f;
    
    internal readonly List<string>  scrollAreaButtons = [];
}