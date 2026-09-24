namespace TuiTerminal;

public class AppState
{
    internal    bool    rotateTexture;
    internal    bool    enabled2;
    internal    bool    useTerminalPixels;
    internal    bool    textureScissor;
    internal    float   speed = 0.1f;
    
    internal readonly List<string>  scrollAreaButtons = [];
}