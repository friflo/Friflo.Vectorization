using Friflo.SmGui;
using Friflo.SmGui.OpenTK;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace OpenTkApp;

class OpenTkWindow : GameWindow
{
    private             OpenTkGuiBackend?   guiBackend;
    private             OpenTkBatch?        batch;
    private             SmGuiApp?           app;
    private readonly    CreateGuiApp        createGuiApp;
    
    public static OpenTkWindow Create(string title, int width, int height, CreateGuiApp createGuiApp)
    {
        Vector2 a = (1E-45f, -5.9167414f);
        Vector2 b = (1E-45f, 13.882292f);
        Vector2 c = Vector2.Slerp(a, b, 0);
        Vector2 d = Vector2.Slerp(a, b, 1);

        var res = Vector3.Elerp((1e-45f, 1, 1), (1, 1, 4), 0.3f);

        GameWindowSettings gwSettings = new GameWindowSettings()
        {
            UpdateFrequency = 250,
            // Win32OnSizeMoveBehavior = Win32OnSizeMoveBehavior.SendFrameEvents,
        };

        NativeWindowSettings nwSettings = new NativeWindowSettings()
        {
            API = ContextAPI.OpenGL,
            APIVersion = new Version(3, 3),
            AutoLoadBindings = true,
            Flags = ContextFlags.Debug | ContextFlags.ForwardCompatible,
            IsEventDriven = false,
            Profile = ContextProfile.Core,
            ClientSize = (width, height),
            StartFocused = true,
            StartVisible = true,
            Title = title,
            WindowBorder = WindowBorder.Resizable,
            WindowState = WindowState.Normal,
        };

        return new OpenTkWindow(gwSettings, nwSettings, createGuiApp);
    }

    public OpenTkWindow(GameWindowSettings gwSettings, NativeWindowSettings nwSettings, CreateGuiApp createGuiApp)
        : base(gwSettings, nwSettings)
    {
        this.createGuiApp = createGuiApp;
    }

    protected override void OnLoad()
    {
        base.OnLoad();

        string ver = GLFW.GetVersionString();
        Console.WriteLine($"GLFW version: {ver}");
        
        guiBackend  = new OpenTkGuiBackend(new DefaultGuiAssets());
        batch       = guiBackend.CreateBatch();
        app         = createGuiApp(guiBackend);
        
        Icon = OpenTkUtils.LoadWindowIcon(guiBackend.Assets);
    }

    protected override void OnUnload()
    {
        base.OnUnload();
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);
    }

    float time = 0;

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);

        const float CycleTime = 8.0f;

        time += (float)args.Time;
        if (time > CycleTime) time = 0;

        Color4 color = Color4.FromHsv(new Vector4(time / CycleTime, 1, 1, 1));

        GL.ClearColor(color);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        
        var width  = Bounds.Size.X;
        var height = Bounds.Size.Y;
        
        app?.RenderApp(batch!, width, height);
        batch?.DrawCommandList(width, height);
        

        SwapBuffers();
    }

    protected override void OnFramebufferResize(FramebufferResizeEventArgs e)
    {
        base.OnFramebufferResize(e);

        GL.Viewport(0, 0, e.Width, e.Height);
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
    }

    protected override void OnMove(WindowPositionEventArgs e)
    {
        base.OnMove(e);
    }
}