using System;
using Friflo.SmGui;
using Friflo.SmGui.KNI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace KNI;

// backend:  https://github.com/kniEngine/kni
/// <summary>
/// Minimal <see cref="Game"/> implementation to create a <see href="https://docs.monogame.net/api/Microsoft.Xna.Framework.Game.html">MonoGame Game window</see>.<br/>
/// You can copy this class for customization or for using MonoGame-specific APIs.
/// </summary>
public class KniWindow : Game
{
    private             KniGuiBackend?          guiBackend;
    private             KniBatch?               batch;
    private             SmGuiApp?               app;
    private readonly    CreateGuiApp            createGuiApp;
    private readonly    GraphicsDeviceManager   graphics;

    public KniWindow(string title, int width, int height, CreateGuiApp createGuiApp)
    {
        this.createGuiApp = createGuiApp;
        
        graphics = new GraphicsDeviceManager(this);
        IsMouseVisible = true;
        
        graphics.PreferredBackBufferWidth   = width;
        graphics.PreferredBackBufferHeight  = height;
        graphics.GraphicsProfile            = GraphicsProfile.HiDef;
        
        graphics.SynchronizeWithVerticalRetrace = false;    // disable VSync
        IsFixedTimeStep = false;                            // disable VSync
        InactiveSleepTime = TimeSpan.FromMilliseconds(1);   // optional: low latency message handling of remote clients
        
        Window.AllowUserResizing    = true;
        Window.Title                = title;
    }

    protected override void Initialize()
    {
        guiBackend  = new KniGuiBackend(GraphicsDevice, new DefaultGuiAssets());
        batch       = guiBackend.CreateBatch();
        app         = createGuiApp(guiBackend);
        guiBackend.SetClientActions(new KniClientAction(this, graphics));
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
        var c = app?.ClearValue ?? 0x775f08ff;   // original Color: 0xffcc10ff
        device.Clear(new Color(c.R, c.G, c.B, c.A));
        
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