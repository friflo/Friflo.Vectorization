
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
namespace Tests.TmGui;

public class Tests_TmGui_window1
{
    private bool    mouseCircle;
    private bool    monocraft;
    private bool    enabled2    = true;
    private float   volume      = 0.8f;
    
    private static void EnsureBatchApi(TmBatch batch)
    {
        batch.SetFormatProvider(CultureInfo.InvariantCulture);
    }
    
    /// Result in <see cref="FrameBuffer.CharCells"/>
    [Test]
    public void Tests_TmGui_window1_TUI_char()
    {
        var backend     = new TuiBackend();
        var frameBuffer = new FrameBuffer();
        var batch       = backend.CreateBatch(TuiColorMode.Monochrome);

        long        start   = 0;
        const int   repeat  = 10; // 2_000_000 - 2.2 sec
        
        for (int n = 0; n < repeat; n++)
        {
            backend.NewFrame();
            var gui = batch.BeginGui(1280, 1000);
            
            using (gui.BeginWindow("Window 1", new(200, 200), new(600, 950))) {
                Window1(gui); 
            }
            batch.DrawRectCommandsChar(frameBuffer, 50, 30, '.', "\r\n");
            if (n == 0) start = Mem.GetAllocatedBytes();
        }
        Mem.AssertNoAlloc(start);
        
        Assert.That(frameBuffer.CharCells.Length, Is.EqualTo(1560));
        var screen  = new string(frameBuffer.CharCells);
        var dir     = Path.GetDirectoryName(GetCurrentFilePath())!;
        var tuiFile = $"{dir}/{TestContext.CurrentContext.Test.Name}.txt";
        
        File.WriteAllText(tuiFile, screen, Utf8WithoutBom);
    }
    

    /// Result in <see cref="FrameBuffer.ColorCells"/>
    [Test]
    public void Tests_TmGui_window1_TUI_color()
    {
        var backend     = new TuiBackend();
        var frameBuffer = new FrameBuffer();
        var batch       = backend.CreateBatch(TuiColorMode.Monochrome);

        long        start   = 0;
        const int   repeat  = 10; // 2_000_000 - 3.5 sec
        
        for (int n = 0; n < repeat; n++)
        {
            backend.NewFrame();
            var gui = batch.BeginGui(1280, 1000);
            
            using (gui.BeginWindow("Window 1", new(200, 200), new(600, 950))) {
                Window1(gui); 
            }
            batch.DrawRectCommandsColor(frameBuffer, 50, 30, new TuiColorCell { character = '.' });
            if (n == 0) start = Mem.GetAllocatedBytes();
        }
        Mem.AssertNoAlloc(start);
        Assert.That(batch.Rects.Length, Is.EqualTo(54));
        Assert.That(batch.Texts.Length, Is.EqualTo(183));
        Assert.That(frameBuffer.ColorCells.Length, Is.EqualTo(1500));
        
        var screen = CellsToString(frameBuffer.ColorCells, 50, 30);
        
        var dir     = Path.GetDirectoryName(GetCurrentFilePath())!;
        var tuiFile = $"{dir}/{TestContext.CurrentContext.Test.Name}.txt";
        
        File.WriteAllText(tuiFile, screen, Utf8WithoutBom);
    }
    
    private static string CellsToString(ReadOnlySpan<TuiColorCell> cells, int targetWidth, int targetHeight)
    {
        int stride      = targetWidth + 2;
        int charCount   = stride * targetHeight;
        var buffer      = new char[charCount].AsSpan();

        for (int line = 0; line < targetHeight; line++) {
            var start = line * stride;
            for (int col = 0; col < targetWidth; col++) {
                buffer[start + col] = cells[line * targetWidth + col].character;
            }
            buffer[start + targetWidth]     = '\r';
            buffer[start + targetWidth + 1] = '\n';
        }
        return new string(buffer);
    }
    
    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    
    // Compiler automatically injects the absolute path of the source file at compile-time
    private static string GetCurrentFilePath([CallerFilePath] string path = "") {
        return path;
    }
    
    [Test]
    public void Tests_TmGui_window1_headless()
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
        Assert.That(verticesLen,        Is.EqualTo(3108));
        
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
            gui.SetNextDefaultFocus();
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