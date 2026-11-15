using Friflo.SmGui;
using Friflo.SmGui.MonoGame;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

// ReSharper disable PrivateFieldCanBeConvertedToLocalVariable
namespace SmGui.MonoGame;

public class GameApp : Game
{
    private readonly    GraphicsDeviceManager   graphics;
//  private             SpriteBatch             spriteBatch;
    
    private             MonoGameGuiBackend?     guiBackend;
    private             MonoGameBatch?          batch;
    private             SmGuiApp?               app;
    private readonly    CreateGuiApp            createGuiApp;

    public GameApp(string title, int width, int height, CreateGuiApp createGuiApp)
    {
        this.createGuiApp = createGuiApp;
        
        graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        
        graphics.SynchronizeWithVerticalRetrace = false;
        graphics.PreferredBackBufferWidth  = width;
        graphics.PreferredBackBufferHeight = height;
        
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
        GraphicsDevice.Clear(Color.CornflowerBlue);
        
        var displayMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        app?.RenderApp(batch!, displayMode.Width, displayMode.Height);

        batch?.DrawCommandList(GraphicsDevice);

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