using Friflo.SmGui;
using Friflo.SmGui.MonoGame;
using Microsoft.Xna.Framework;

namespace SmGui.MonoGame;

/// <summary>
/// Minimal <see cref="Game"/> implementation to create a <see href="https://docs.monogame.net/api/Microsoft.Xna.Framework.Game.html">MonoGame Game window</see>.<br/>
/// You can copy this class for customization or for using MonoGame-specific APIs.
/// </summary>
public class GameApp : Game
{
    // private          SpriteBatch         spriteBatch; // not required for SmGui
    private             MonoGameGuiBackend? guiBackend;
    private             MonoGameBatch?      batch;
    private             SmGuiApp?           app;
    private readonly    CreateGuiApp        createGuiApp;

    public GameApp(string title, int width, int height, CreateGuiApp createGuiApp)
    {
        this.createGuiApp = createGuiApp;
        
        var graphics = new GraphicsDeviceManager(this);
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

        base.Initialize();
    }

    protected override void LoadContent()
    {
        // spriteBatch = new SpriteBatch(GraphicsDevice);
    }

    protected override void Update(GameTime gameTime)
    {
        // Optional: Input-Poll für das Backend
        // guiBackend.PollInput();

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        var device = GraphicsDevice;
        device.Clear(Color.CornflowerBlue);
        
        var parameters  = device.PresentationParameters;
        app?.RenderApp(batch!, parameters.BackBufferWidth, parameters.BackBufferHeight);

        batch?.DrawCommandList(device);

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