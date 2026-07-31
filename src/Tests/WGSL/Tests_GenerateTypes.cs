using Friflo.WGSL.Transpiler.CSharp;
using Friflo.WGSL.Transpiler.WGSL;
using NUnit.Framework;


// ReSharper disable once InconsistentNaming
namespace Tests.WGSL;

public static class Tests_GenerateTypes
{
    [Test]
    public static void Tests_WGSL_GenerateAllTypes()
    {
        var projectDir = TestWgslUtils.GetProjectDir();

        // var mappings = new  WgslType2CSharpType[] { new (CsTypeCode.vec2i, "CustomTypes", "Vector2i") };
        var mappings = TypeMappings.LoadTypeMappings($"{projectDir}/{TypeMappings.MappingPath}", out var errors);
        if (errors.Length > 0) {
            foreach (var error in errors) {
                Assert.Fail($"line: {error.line} - {error.message}");
            }
        }
        Assert.NotNull(mappings);
        Assert.That(mappings.Length, Is.EqualTo(5));
        Assert.That(mappings, Has.Member(new TypeMapping(CsTypeCode.vec2i,   "CustomTypes",         "Vector2i")));
        Assert.That(mappings, Has.Member(new TypeMapping(CsTypeCode.vec2u,   "CustomTypes",         "Vector2<uint>")));
        
        Assert.That(mappings, Has.Member(new TypeMapping(CsTypeCode.mat2x2h, "OpenTK.Mathematics",  "Matrix2")));
        Assert.That(mappings, Has.Member(new TypeMapping(CsTypeCode.mat2x3h, "Silk.NET.Maths",      "Matrix2x3<Half>")));
        Assert.That(mappings, Has.Member(new TypeMapping(CsTypeCode.mat2x4f, "Unity.Mathematics",   "float2x4")));

        var files = WgslUtils.LoadShaderFilesRecursive(projectDir);
        
        for (int n = 0; n < 1; n++) {
            var typeEmitter = new TypeGen();
            typeEmitter.EmitAllStructs(files, projectDir, mappings, errors);
        }
    }
}