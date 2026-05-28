using System;
using System.IO;
using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Vectorization;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;
using Friflo.Vectorization.WebGPU.Runtime;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;


namespace Tests.Generators;

public static class VerifyUtils
{
    public static Compilation CreateCompilation(string source)
    {
        // 1. Get the directory where the core libraries live
        var assemblyPath = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        return CSharpCompilation.Create(
            assemblyName: "TestProj",
            syntaxTrees: new[] { CSharpSyntaxTree.ParseText(source) },
            references: new[] {
                MetadataReference.CreateFromFile(typeof(Instance)           .Assembly.Location),    // Friflo.Vectorization.WebGPU.Runtime
                MetadataReference.CreateFromFile(typeof(WgpuInstance)       .Assembly.Location),    // Friflo.Vectorization.WebGPU
                MetadataReference.CreateFromFile(typeof(GpuInstance)        .Assembly.Location),    // Friflo.Vectorization.GPU
                MetadataReference.CreateFromFile(typeof(VectorizeAttribute) .Assembly.Location),    // Friflo.Vectorization.Attributes
                MetadataReference.CreateFromFile(typeof(IComponent)         .Assembly.Location),    // Friflo.Engine.ECS
                MetadataReference.CreateFromFile(typeof(MathF)              .Assembly.Location),    // System
                MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Numerics.dll")),
                MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Numerics.Vectors.dll")),
                
                // 2. The 'Contract' assemblies (The Maps)
                MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Runtime.dll")), // Fixes Attribute/ValueType
                MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "mscorlib.dll")),       // Fixes legacy types
                MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "netstandard.dll")),    // Fixes library compatibility
                
                MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Runtime.dll")),
                MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Runtime.Intrinsics.dll"))
            },
            options: new CSharpCompilationOptions(
                outputKind: OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: true));
    }
    
    public static void CheckOutputCompilation(Compilation outputCompilation)
    {
        var compileErrors = outputCompilation.GetDiagnostics()
        .Where(d => d.Severity == DiagnosticSeverity.Error)
        .ToList();
        if (compileErrors.Any()) {
            var errorMessages = string.Join("\n", compileErrors.Select(e => e.GetMessage()));
            throw new Exception($"Generated code failed to compile:\n{errorMessages}");
        }
    }

    public static bool IgnoreStaticSource(GeneratedSourceResult result)
    {
        if (result.HintName.Equals("Friflo.Vectorization.Intrinsics/MathUtils.g.cs")   ||
            result.HintName.Equals("Friflo.Vectorization.Intrinsics/AvxVector2.g.cs")  ||
            result.HintName.Equals("Friflo.Vectorization.Intrinsics/AvxVector3.g.cs")  ||
            result.HintName.Equals("Friflo.Vectorization.Intrinsics/AvxVector4.g.cs")  ||
            result.HintName.Equals("Friflo.Vectorization.Intrinsics/VectorUtils.g.cs"))
        {
            return true;
        }
        return false;
    } 
}