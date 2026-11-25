using Friflo.SmGui;
using Friflo.SmGui.OpenTK;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable RedundantOverriddenMember
namespace OpenTK;

// backend:  https://github.com/opentk/opentk
/// <summary>
/// Minimal <see cref="GameWindow"/> implementation based on <see href="https://github.com/opentk/opentk/blob/master/tests/LocalTest/Program.cs">OpenTK LocalTest GameWindow</see>.<br/>
/// You can copy this class for customization or for using OpenTK-specific APIs.
/// </summary>
class OpenTkWindow : GameWindow
{
    private             OpenTkGuiBackend?   guiBackend;
    private             OpenTkBatch?        batch;
    private             SmGuiApp?           app;
    private readonly    CreateGuiApp        createGuiApp;
    
    public static OpenTkWindow Create(string title, int width, int height, CreateGuiApp createGuiApp)
    {
        GameWindowSettings gwSettings = new GameWindowSettings()
        {
            UpdateFrequency = 0, // 0: max fps, otherwise fps
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
            StartVisible = false,
            Title = title,
            WindowBorder = WindowBorder.Resizable,
            WindowState = WindowState.Normal,
        };

        var window = new OpenTkWindow(gwSettings, nwSettings, createGuiApp);
        window.CenterWindow();
        window.IsVisible = true;
        return window;
    }

    private OpenTkWindow(GameWindowSettings gwSettings, NativeWindowSettings nwSettings, CreateGuiApp createGuiApp)
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
        
        guiBackend.SetClientActions(new OpenTkClientAction(this));
        if (OperatingSystem.IsWindows()) {
            Icon = OpenTkUtils.LoadWindowIcon(guiBackend.Assets); // fail on macOS
        }
    }

    protected override void OnUnload()
    {
        app?.Dispose();
        app = null;
        batch?.Dispose();
        batch = null;
        guiBackend?.Dispose();
        guiBackend = null;
        
        base.OnUnload();
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);

        var color =new Color4(0x35 / 255f, 0x6A / 255f, 0xA7 / 255f, 1.0f); // color from OpenTK icon
        GL.ClearColor(color);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        
        var width  = ClientSize.X;
        var height = ClientSize.Y;
        
        OpenTkInput.UpdateInput(guiBackend, this);
        guiBackend?.NewFrame();
        app?.RenderApp(batch!, width, height);
        batch?.DrawCommandList(width, height);
        OpenTkInput.SetMouseCursor(guiBackend, this);

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