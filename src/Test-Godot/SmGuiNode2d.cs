using Godot;
using Friflo.SmGui;
using Friflo.SmGui.Godot;
using GuiDemo;

public partial class SmGuiNode2d : Node2D
{
    private GodotGuiBackend?    guiBackend;
    private GodotBatch?         batch;
    private SmGuiApp?           app;
    
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
        guiBackend  = new GodotGuiBackend(new DefaultGuiAssets());
        batch       = guiBackend.CreateBatch();
        app         = new DemoGuiApp(guiBackend, false);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
        // Get the current viewport size dynamically
        var viewportSize = GetViewportRect().Size;
        var width  = (int)viewportSize.X;
        var height = (int)viewportSize.Y;
        
        // Render the application frame
        app?.RenderApp(batch!, width, height);              
        
        // Pass GetCanvasItem() as the target canvas item for Godot's RenderingServer
        batch?.DrawCommandList(GetCanvasItem(), 0, 0, width, height);
	}
    
    public override void _ExitTree()
    {
        // Clean up RenderingServer resources to prevent memory leaks
        batch?.FreeCanvasItems();
    }
}
