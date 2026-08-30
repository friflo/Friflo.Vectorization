
using System;
using System.Globalization;
using Friflo.ImGui;
using Friflo.ImGui.Headless;
using NUnit.Framework;
using Tests.Utils;


// ReSharper disable ArrangeObjectCreationWhenTypeNotEvident
// ReSharper disable once InconsistentNaming
namespace Tests.ImDraw;

public class Tests_ImDraw_headless
{
    private bool    mouseCircle;
    private bool    monocraft;
    private bool    enabled2;
    private float   volume;
    
    
    
    private static void EnsureBatchApi(ImBatch batch)
    {
        batch.SetFormatProvider(CultureInfo.InvariantCulture);
        
    }
    
    [Test]
    public void Tests_ImDraw_headless_window1()
    {
        var         backend = new HeadlessBackend();
        var         batch   = backend.CreateBatch();
        EnsureBatchApi(batch);
        long        start = 0;
        const int   repeat  = 10; // 100_000 - 0.98 sec    bottleneck: FillArc() - GuiSizes.CornerSegments = 4
        
        for (int n = 0; n < repeat; n++)
        {
            var gui = batch.BeginGui(1280, 1000);
            
            using (gui.BeginWindow("Window 1", new(100, 20), new(400, 950))) {
                Window1(gui); 
            }
            batch.DrawCommandList();
            if (n == 0) start = Mem.GetAllocatedBytes();
        }
        
        Mem.AssertNoAlloc(start);
        var drawList    = batch.DrawList;
        var verticesLen = batch.Vertices.Length;
        Assert.That(drawList.Length,    Is.EqualTo(2));
        Assert.That(verticesLen,        Is.EqualTo(1608));
        
        int vertexSum = 0;
        int indexSum  = 0;
        foreach (var cmd in drawList) {
            vertexSum += cmd.vertexView.length;
            indexSum  += cmd.indexView.length;
        }
        Assert.That(vertexSum, Is.EqualTo(verticesLen));
        Assert.That(indexSum,  Is.EqualTo(verticesLen * 6 / 4));
    }
    
    private readonly GuiStyle redButtonStyle = new() {
        colors = new GuiColors {
            ButtonColor = 0xaa4444ff,
            ButtonHover = 0xcc6666ff,
            ButtonDown  = 0x882222ff,
            ButtonText  = 0xffffffff,
            FocusColor  = 0xffffffff,
        }
    };
    
    private readonly GuiStyle greenButtonStyle = new() {
        colors = new GuiColors {
            ButtonColor = 0x229922ff,
            ButtonHover = 0x44bb44ff,
            ButtonDown  = 0x007700ff,
            ButtonText  = 0xffffffff,
            FocusColor  = 0xffffffff,
        }
    };
    
    private void Window1(Gui gui)
    {
        gui.Label("hello GUI");
        gui.Spacer();
        using (gui.PushStyle(greenButtonStyle)) {
            if (gui.Button("hello"))                            Console.WriteLine("Clicked: hello");
        }

        gui.Spacer();
        gui.Checkbox("mouse circle", ref mouseCircle);
        if(gui.Checkbox("Monocraft", ref monocraft)) {
        }
        gui.Spacer();
        if (gui.Slider("Volume", ref volume, 0f, 1f, 200)) Console.WriteLine($"Volume: changed");
        gui.Spacer();
        
        gui.BeginHorizontal();
            if (gui.Button("First"))                            Console.WriteLine("Clicked: First");
            gui.Spacer(10);
            if (gui.Button("Second"))                           Console.WriteLine("Clicked: Second");
            gui.Spacer(10);
            if (gui.Button("Red", style: redButtonStyle))       Console.WriteLine("Clicked: Red");
        gui.EndHorizontal();
        
        gui.Label("after horizontal");
        using (var space = gui.BeginSpace(new(64, 64), "sprite")) {
            if (space.isFired) Console.WriteLine("Clicked: Sprite");
        }
        gui.Spacer();
        gui.Checkbox("checkbox", ref enabled2);
        
        gui.Spacer();
        
        using (gui.BeginHorizontalAligned(47, HorizontalAlignment.Right)) {
            gui.Button("Right");
            gui.Button(" A´");
            gui.Button(" B´");
            gui.Button(" C´ ");
        }
        using (gui.BeginHorizontalAligned(11, HorizontalAlignment.Center)) {
            gui.Button("Center");
            gui.Button(" 1 ");
            gui.Button(" 2 ");
            gui.Button(" 3 ");
        }
        using (gui.BeginHorizontal()) {
            gui.Button("usual");
            gui.Button("horizontal");
            gui.Button(" A ");
            gui.Button(" B ");
            gui.Button(" C ");
        }
    }
}