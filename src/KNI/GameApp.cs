using Friflo.SmGui;
using Friflo.SmGui.KNI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace KNI;

/// <summary>
/// Minimal <see cref="Game"/> implementation to create a <see href="https://docs.monogame.net/api/Microsoft.Xna.Framework.Game.html">MonoGame Game window</see>.<br/>
/// You can copy this class for customization or for using MonoGame-specific APIs.
/// </summary>
public class GameApp : Game
{
    // private          SpriteBatch         spriteBatch; // not required for SmGui
    private             KniGuiBackend?      guiBackend;
    private             KniBatch?           batch;
    private             SmGuiApp?           app;
    private readonly    CreateGuiApp        createGuiApp;

    public GameApp(string title, int width, int height, CreateGuiApp createGuiApp)
    {
        this.createGuiApp = createGuiApp;
        
        var graphics = new GraphicsDeviceManager(this);
        IsMouseVisible = true;
        
        graphics.PreferredBackBufferWidth   = width;
        graphics.PreferredBackBufferHeight  = height;
        graphics.GraphicsProfile            = GraphicsProfile.HiDef;
        
        graphics.SynchronizeWithVerticalRetrace = false;    // disable VSync
        IsFixedTimeStep = false;                            // disable VSync
        
        Window.AllowUserResizing    = true;
        Window.Title                = title;
    }

    protected override void Initialize()
    {
        guiBackend  = new KniGuiBackend(GraphicsDevice, new DefaultGuiAssets());
        batch       = guiBackend.CreateBatch();
        app         = createGuiApp(guiBackend);

        base.Initialize();
    }

    protected override void Draw(GameTime gameTime)
    {
        var device = GraphicsDevice;
        device.Clear(Color.CornflowerBlue);
        
        var width   = device.PresentationParameters.BackBufferWidth;
        var height  = device.PresentationParameters.BackBufferHeight;
        KniInput.UpdateInput(guiBackend);
        guiBackend?.NewFrame();
        app?.RenderApp(batch!, width, height);
        batch?.DrawCommandList(device, 0, 0, width, height);
        KniInput.SetMouseCursor(guiBackend);

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