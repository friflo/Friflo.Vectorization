using System;
using System.IO;
using Friflo.WGSL.Transpiler.WGSL;

namespace Friflo.WGSL.Transpiler;

public static class Program
{
    public static int Main(string[] args)
    {
        // for (int n = 0; n < 3; n++) { Console.WriteLine("WGSL : error : ----- test log is shown -----"); }
        try
        {
            var projectDir  = Directory.GetCurrentDirectory();
            var file        = $"{projectDir}/{TypeMappings.MappingPath}";
            
            var mappings = TypeMappings.LoadTypeMappings(file, out var error);
            if (error.IsSet) {
                var line = error.line.HasValue ? $"({error.line})" : string.Empty;
                Console.WriteLine($"{file}{line}: error {error.code}: {error.message}");
                return 100;
            }
            var files = WgslUtils.LoadAdditionalFilesRecursive($"{projectDir}/shaders");
            
            var typeEmitter = new TypeGen();
            typeEmitter.EmitAllStructs(files, projectDir, mappings, error);
        }
        catch (Exception exception) {
            Error(exception.ToString());
            return 101;
        }
        return 0; // use 111 to see if Transpiler.dll is found and Main() is executed 
    }
    
    private static void  Error(string message)
    {
         Console.WriteLine($"WGSL : error : {message}");
    }
}