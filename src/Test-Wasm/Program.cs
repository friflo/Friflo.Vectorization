using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

// requires one-time activation of developer certificate:
//   dotnet dev-certs https --trust

[SupportedOSPlatform("browser")]
partial class Program
{
    static void Main(string[] args)
    {
        // runs in Browser-Tab
        Console.WriteLine(".NET WebAssembly Core Runtime initialized.");
    }

    [JSExport]
    public static string ProcessData(string input)
    {
        return $"C# processed: {input}";
    }
}