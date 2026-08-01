using System;
using System.Numerics;
using Friflo.Vectorization.WebGPU;
using Friflo.WGSL.Transpiler.CSharp;
using Friflo.WGSL.Transpiler.WGSL;
using NUnit.Framework;

// ReSharper disable UseObjectOrCollectionInitializer
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
    
    
    [Test]
    public static void Tests_WGSL_generated_fixed_size_array()
    {
        var array = new Vector4_UniArr_8();
        array[0] = new Vector4(0, 42, 0, 0);
        array[7] = new Vector4(7, 42, 0, 0);
        
        Assert.That(array.Length,   Is.EqualTo(8));
        Assert.That(array[0],       Is.EqualTo(new Vector4(0, 42, 0, 0)));
        Assert.That(array[7],       Is.EqualTo(new Vector4(7, 42, 0, 0)));


        int step = 0;
        foreach (ref var item in array)
        {
            switch (step) {
                case 0:  Assert.That(item, Is.EqualTo(new Vector4(0, 42, 0, 0))); break;
                case 7:  Assert.That(item, Is.EqualTo(new Vector4(7, 42, 0, 0))); break;
            }
            step++;
        }
        Assert.That(step, Is.EqualTo(8));
        
        var enumerator = array.GetEnumerator();
        var current = enumerator.Current;  // Direct call return first element
        Assert.That(current, Is.EqualTo(new Vector4(0, 42, 0, 0)));
        
        while (enumerator.MoveNext()) {
            _ = enumerator.Current;
        }
        var last = enumerator.Current;
        Assert.That(last, Is.EqualTo(new Vector4(7, 42, 0, 0)));
        
        Assert.Throws<IndexOutOfRangeException>(() => _ = array[-1]);
        Assert.Throws<IndexOutOfRangeException>(() => _ = array[8]);
        
        
        var debugView = new FixedArrayDebugView<Vector4>(array);
        var items = debugView.Items;
        Assert.That(items.Length, Is.EqualTo(8));
        Assert.That(items[0], Is.EqualTo(new Vector4(0, 42, 0, 0)));
        Assert.That(items[7], Is.EqualTo(new Vector4(7, 42, 0, 0)));
    }
}