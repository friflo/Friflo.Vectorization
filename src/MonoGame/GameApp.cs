using Friflo.SmGui;
using Friflo.SmGui.MonoGame;
using Microsoft.Xna.Framework;

namespace MonoGame;

/// <summary>
/// Minimal <see cref="Game"/> implementation to create a <see href="https://docs.monogame.net/api/Microsoft.Xna.Framework.Game.html">MonoGame Game window</see>.<br/>
/// You can copy this class for customization or for using MonoGame-specific APIs.
/// </summary>
public class GameApp : Game
{
    private             MonoGameGuiBackend?     guiBackend;
    private             MonoGameBatch?          batch;
    private             SmGuiApp?               app;
    private readonly    CreateGuiApp            createGuiApp;
    private readonly    GraphicsDeviceManager   graphics;

    public GameApp(string title, int width, int height, CreateGuiApp createGuiApp)
    {
        this.createGuiApp = createGuiApp;
        
        graphics = new GraphicsDeviceManager(this);
        IsMouseVisible = true;
        
        graphics.PreferredBackBufferWidth  = width;
        graphics.PreferredBackBufferHeight = height;
        
        graphics.SynchronizeWithVerticalRetrace = false;    // disable VSync
        IsFixedTimeStep = false;                            // disable VSync
        
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

    protected override void Draw(GameTime gameTime)
    {
        var device = GraphicsDevice;
        device.Clear(Color.CornflowerBlue);
        
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