using NUnit.Common;
using NUnitLite;
using System.Text;
using Tests.GPU;


namespace TestsAndroid;

internal static class TestRunner
{
    public static void Run()
    {
        var strWriter = new StringWriter();
        // var autoRun = new AutoRun(Assembly.GetExecutingAssembly());
        
        var autoRun = new AutoRun(typeof(TestCompute).Assembly);

        // Execute tests and redirect output to our StringWriter
        var nunitWriter = new NUnitTextWriter(strWriter);
        autoRun.Execute(new string[] { }, nunitWriter, null);

        var finalResult = strWriter.ToString();
    }
}


public class NUnitTextWriter : ExtendedTextWriter
{
    private readonly TextWriter _writer;
    public NUnitTextWriter(TextWriter writer) => _writer = writer;

    // Standard overrides
    public override void Write(char value) => _writer.Write(value);
    public override void Write(string? value) => _writer.Write(value);
    public override void WriteLine(string? value) => _writer.WriteLine(value);
    public override Encoding Encoding => _writer.Encoding;

    // NUnit Color/Label overrides - Redirecting all to plain text
    public override void Write(ColorStyle style, string value) => _writer.Write(value);
    public override void WriteLine(ColorStyle style, string value) => _writer.WriteLine(value);

    public override void WriteLabel(string label, object option)
        => _writer.Write($"{label}{option}");

    public override void WriteLabel(string label, object option, ColorStyle valueStyle)
        => _writer.Write($"{label}{option}");

    public override void WriteLabelLine(string label, object option)
        => _writer.WriteLine($"{label}{option}");

    public override void WriteLabelLine(string label, object option, ColorStyle valueStyle)
        => _writer.WriteLine($"{label}{option}");
}
