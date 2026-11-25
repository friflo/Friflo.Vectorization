using System;
using Friflo.SmGui;
using Friflo.SmGui.MonoGame;
using Microsoft.Xna.Framework;

namespace MonoGame;

// backend:  https://github.com/monogame/monogame
/// <summary>
/// Minimal <see cref="Game"/> implementation to create a <see href="https://docs.monogame.net/api/Microsoft.Xna.Framework.Game.html">MonoGame Game window</see>.<br/>
/// You can copy this class for customization or for using MonoGame-specific APIs.
/// </summary>
public class MonoGameWindow : Game
{
    private             MonoGameGuiBackend?     guiBackend;
    private             MonoGameBatch?          batch;
    private             SmGuiApp?               app;
    private readonly    CreateGuiApp            createGuiApp;
    private readonly    GraphicsDeviceManager   graphics;

    public MonoGameWindow(string title, int width, int height, CreateGuiApp createGuiApp)
    {
        this.createGuiApp = createGuiApp;
        
        graphics = new GraphicsDeviceManager(this);
        IsMouseVisible = true;
        
        graphics.PreferredBackBufferWidth  = width;
        graphics.PreferredBackBufferHeight = height;
        
        graphics.SynchronizeWithVerticalRetrace = false;    // disable VSync
        IsFixedTimeStep = false;                            // disable VSync
        InactiveSleepTime = TimeSpan.FromMilliseconds(1);   // optional: low latency message handling of remote clients
        
        Window.AllowUserResizing    = true;
        Window.Title                = title;
    }

    protected override void Initialize()
    {
        guiBackend  = new MonoGameGuiBackend(GraphicsDevice, new DefaultGuiAssets());
        batch       = guiBackend.CreateBatch();
        app         = createGuiApp(guiBackend);
        guiBackend.SetClientActions(new MonoGameClientAction(this, graphics));
        base.Initialize();
    }

    protected override void Update(GameTime gameTime)
    {
        app?.SessionLoop?.IterateSessions(); // set InactiveSleepTime = 1 ms for low latency message handling of remote clients
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        var device = GraphicsDevice;
        var c = app?.ClearValue ?? Color32.CornflowerBlue;
        device.Clear(new Color(c.R, c.G, c.B, c.A));
        
        var width   = device.PresentationParameters.BackBufferWidth;
        var height  = device.PresentationParameters.BackBufferHeight;
        MonoGameInput.UpdateInput(guiBackend);
        guiBackend?.NewFrame();
        app?.RenderApp(batch!, width, height);
        batch?.DrawCommandList(device, 0, 0, width, height);
        MonoGameInput.SetMouseCursor(guiBackend);

        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        app?.Dispose();
        app = null;
        batch?.Dispose();
        batch = null;
        guiBackend?.Dispose();
        guiBackend = null;
        
        base.UnloadContent();
    }
}