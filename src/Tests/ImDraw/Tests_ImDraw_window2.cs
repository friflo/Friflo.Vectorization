
using Friflo.ImGui2D;
using Friflo.ImGui2D.Headless;
using NUnit.Framework;
using Tests.Utils;


// ReSharper disable ArrangeObjectCreationWhenTypeNotEvident
// ReSharper disable once InconsistentNaming
namespace Tests.ImDraw;

public class Tests_ImDraw_window2
{
    [Test]
    public void Tests_ImDraw_window2_headless()
    {
        var         backend = new HeadlessBackend();
        var         batch   = backend.CreateBatch();

        long        start   = 0;
        const int   repeat  = 10; // 500_000 - 7.9 sec
        
        for (int n = 0; n < repeat; n++)
        {
            var gui = batch.BeginGui(1280, 1000);
            
            using (gui.BeginWindow("Window 2", new(100, 20), new(400, 950))) {
                Window2(gui); 
            }
            batch.DrawCommandList();
            if (n == 0) start = Mem.GetAllocatedBytes();
        }
        
        Mem.AssertNoAlloc(start);
        var drawList    = batch.DrawList;
        var verticesLen = batch.Vertices.Length;
        Assert.That(drawList.Length,    Is.EqualTo(10));
        Assert.That(verticesLen,        Is.EqualTo(3180));
    }
    

    
    private static void Window2(Gui gui)
    {
        gui.Label("fixed child");
        using (gui.BeginChild(1, Dim.Fill_X(0, 90))) {
            gui.Button("Button 1 clipped", Dim.Fill_X(0, Fit.Content));
            gui.Button("Button 2 clipped");
        }
        gui.Spacer();
        gui.Label("auto-fit child");
        using (gui.BeginChild(2, Dim.Fill())) {
            gui.Button("Button 1 unclipped",  Dim.Fill_X(0, Fit.Content));
            gui.Button("Button 2 unclipped");
        }
        gui.Spacer();
        gui.Label("scroll area");
        var scrollArea = gui.BeginScrollArea(3, Dim.Fill());
            gui.Button("Button 1 - more to to enable horizontal scrolling");
            gui.Button("Button 2 -  Dim.Fill_X(0, Fit.Content)",  Dim.Fill_X(0, Fit.Content));
            
            var area2  = gui.BeginScrollArea(4, Dim.Fill_X(0, 200));
                gui.Button("Sub 1");
                gui.Button("Sub 2 -  Dim.Fill_X(0, Fit.Content)",  Dim.Fill_X(0, Fit.Content));
                gui.Button("Sub 3 - size: new(-10, 0)",  Dim.Fill_X(10, Fit.Content));
                gui.Button("Sub 4");
                gui.Button("Sub 5");
                gui.Button("Sub 6");
                gui.Button("Sub last");
            gui.EndScrollArea(area2);
            
            gui.Button("Button 3");
            gui.Button("Button 4");
            gui.BeginHorizontal();
                gui.Button("Hori A");
                gui.Button("Hori B");
                gui.Button("Hori C");
                gui.Button("Hori D");
                gui.Button("Hori E");
                gui.Button("Hori F");
                gui.Button("Hori G");
                gui.Button("Hori H");
            gui.EndHorizontal();
            gui.Button("Button 5");
            gui.Button("Button 6");
            gui.Button("Button 7");
            gui.Button("Button 8");
            using (gui.BeginHorizontalAligned(123, HorizontalAlignment.Right)) {
                gui.Button("Right");
            }
            using (gui.BeginHorizontalAligned(456, HorizontalAlignment.Center)) {
                gui.Button("Center");
            }
            gui.Button("Button last");
        
        gui.EndScrollArea(scrollArea);
    }
}