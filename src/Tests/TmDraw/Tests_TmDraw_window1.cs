
using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Friflo.TmGui;
using Friflo.TmGui.Headless;
using Friflo.TmGui.TUI;
using NUnit.Framework;
using Tests.Utils;


// ReSharper disable ArrangeObjectCreationWhenTypeNotEvident
// ReSharper disable once InconsistentNaming
namespace Tests.TmDraw;

public class Tests_TmDraw_window1
{
    private bool    mouseCircle;
    private bool    monocraft;
    private bool    enabled2;
    private float   volume;
    
    private static void EnsureBatchApi(TmBatch batch)
    {
        batch.SetFormatProvider(CultureInfo.InvariantCulture);
    }
    
    /// Result in <see cref="TuiBackend.FrameBuffer"/>
    [Test]
    public void Tests_TmDraw_window1_TUI_char()
    {
        var backend = new TuiBackend();
        var batch   = backend.CreateBatch();

        long        start   = 0;
        const int   repeat  = 10; // 2_000_000 - 2.2 sec
        
        for (int n = 0; n < repeat; n++)
        {
            var gui = batch.BeginGui(1280, 1000);
            
            using (gui.BeginWindow("Window 1", new(200, 200), new(600, 950))) {
                Window1(gui); 
            }
            batch.DrawRectCommandsChar(50, 30);
            if (n == 0) start = Mem.GetAllocatedBytes();
        }
        Mem.AssertNoAlloc(start);
        
        Assert.That(backend.FrameBuffer.Length, Is.EqualTo(1530));
        var screen  = new string(backend.FrameBuffer);
        var dir     = Path.GetDirectoryName(GetCurrentFilePath())!;
        var tuiFile = $"{dir}/{TestContext.CurrentContext.Test.Name}.txt";
        
        File.WriteAllText(tuiFile, screen, Utf8WithoutBom);
    }
    

    /// Result in <see cref="TuiBackend.FrameBufferColor"/>
    [Test]
    public void Tests_TmDraw_window1_TUI_color()
    {
        var backend = new TuiBackend();
        var batch   = backend.CreateBatch();

        long        start   = 0;
        const int   repeat  = 10; // 2_000_000 - 3.8 sec
        
        for (int n = 0; n < repeat; n++)
        {
            var gui = batch.BeginGui(1280, 1000);
            
            using (gui.BeginWindow("Window 1", new(200, 200), new(600, 950))) {
                Window1(gui); 
            }
            batch.DrawRectCommandsColor(50, 30);
            if (n == 0) start = Mem.GetAllocatedBytes();
        }
        Mem.AssertNoAlloc(start);
        
        var buffer = backend.FrameBufferColor;
        Assert.That(buffer.Length, Is.EqualTo(1530));
        
        var chars = new char[buffer.Length];
        for (int i = 0; i < buffer.Length; i++) {
            chars[i] = buffer[i].character;
        }
        var screen  = new string(chars);
        var dir     = Path.GetDirectoryName(GetCurrentFilePath())!;
        var tuiFile = $"{dir}/{TestContext.CurrentContext.Test.Name}.txt";
        
        File.WriteAllText(tuiFile, screen, Utf8WithoutBom);
    }
    
    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    
    // Compiler automatically injects the absolute path of the source file at compile-time
    private static string GetCurrentFilePath([CallerFilePath] string path = "") {
        return path;
    }
    
    [Test]
    public void Tests_TmDraw_window1_headless()
    {
        var         backend = new HeadlessBackend();
        var         batch   = backend.CreateBatch();
        EnsureBatchApi(batch);
        long        start   = 0;
        const int   repeat  = 10; // 500_000 - 5.2 sec    bottleneck: FillArc() - GuiSizes.CornerSegments = 3
        
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
        Assert.That(verticesLen,        Is.EqualTo(3064));
        
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