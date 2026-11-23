using Demos.BasicDraw;
using Demos.BasicGui;
using Godot;
using Friflo.SmGui;
using Friflo.SmGui.Godot;


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
        app         = new BasicGuiApp(guiBackend);
        // app         = new BasicDrawApp(guiBackend);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
        // Get the current viewport size dynamically
        var viewportSize = GetViewportRect().Size;
        var width  = (int)viewportSize.X;
        var height = (int)viewportSize.Y;
        
        app?.RenderApp(batch!, width, height);              
        
        var canvasTarget = GetCanvasItem();
        batch?.DrawCommandList(canvasTarget, 0, 0, width, height);
	}
    
    public override void _ExitTree()
    {
        // Clean up RenderingServer resources to prevent memory leaks
        batch?.FreeCanvasItems();
    }
}
