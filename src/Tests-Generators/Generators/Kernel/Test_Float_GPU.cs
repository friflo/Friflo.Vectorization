// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using Friflo.Vectorization;
using Friflo.Vectorization.GPU;
using NUnit.Framework;

// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Kernel.Generators;


public partial class Test_Float_GPU : KernelBase
{
    private readonly float[]    scalar1 = new float[128];
    private readonly float[]    scalar2 = new float[128];
    private readonly float[]    buffer1 = new float[128];
    private readonly float[]    buffer2 = new float[128];
    
    // ----------------------------------------------
    [Kernel] [OmitHash]
    private static void Multiply([Span] ref float position, [Span] float velocity) {
        position *= velocity;
    }
        
    [Test]
    public void Test_Kernel_Multiply()
    {
        for (int n = 0; n < 128; n++) {
            scalar1[n] = buffer1[n] = n;
            scalar2[n] = buffer2[n] = n + 100;
        }
        using var gpuBuffer1   = Device.CreateBuffer(buffer1, GpuBufferUsage.Storage | GpuBufferUsage.CopySrc, "position");
        using var gpuBuffer2   = Device.CreateBuffer(buffer2, GpuBufferUsage.Storage, "velocity");        

        MultiplyVector(scalar1,    		 scalar2, false);
        MultiplyKernel(gpuBuffer1.InOut, gpuBuffer2.In);
        
        Device.Wait(gpuBuffer1);
        
        gpuBuffer1.Download(gpuBuffer1, buffer1);
        
        for (int n = 0; n < 128; n++) {
            Assert.That(scalar1[n], Is.EqualTo(buffer1[n]));
        }
    }
    
    // ----------------------------------------------
    [Kernel] [OmitHash]
    private static void Assign([Span] ref float position, [Span] float velocity) {
        position = velocity;
    }
    
    [Test]
    public void Test_Kernel_Assign()
    {
        for (int n = 0; n < 128; n++) {
            scalar1[n] = buffer1[n] = n;
            scalar2[n] = buffer2[n] = n + 100;
        }
        De.WriteLine("---- Test_Kernel_Assign 1");
        using var gpuBuffer1   = Device.CreateBuffer(buffer1, GpuBufferUsage.Storage | GpuBufferUsage.CopySrc, "position");
        Console.WriteLine("---- Test_Kernel_Assign 2");     Console.Out.Flush();
        using var gpuBuffer2   = Device.CreateBuffer(buffer2, GpuBufferUsage.Storage, "velocity");
        Console.WriteLine("---- Test_Kernel_Assign 3");     Console.Out.Flush();
        AssignVector(scalar1,          scalar2, false);
        Console.WriteLine("---- Test_Kernel_Assign 4");     Console.Out.Flush();
        AssignKernel(gpuBuffer1.InOut, gpuBuffer2.In);
        Console.WriteLine("---- Test_Kernel_Assign 5");     Console.Out.Flush();
        Device.Wait(gpuBuffer1);
        Console.WriteLine("---- Test_Kernel_Assign 6");     Console.Out.Flush();
        gpuBuffer1.Download(gpuBuffer1, buffer1);
        Console.WriteLine("---- Test_Kernel_Assign 7");     Console.Out.Flush();
        for (int n = 0; n < 128; n++) {
            Assert.That(scalar1[n], Is.EqualTo(buffer1[n]));
        }
    }
    
    // ----------------------------------------------
    [Kernel] [OmitHash]
    private static void Add([Span] ref float dst, [Span] float src) {
        dst += src;
    }
    
    [Test]
    public void Test_Kernel_Buffers()
    {
        for (int n = 0; n < 128; n++) {
            buffer1[n] = n;
            buffer2[n] = n;
        }
        using var gpuDst   = Device.CreateBuffer(buffer1, GpuBufferUsage.Storage | GpuBufferUsage.CopySrc, "dst");
        using var gpuSrc   = Device.CreateBuffer(buffer2, GpuBufferUsage.Storage | GpuBufferUsage.CopySrc, "src");
        BufferView<float>   view1 = gpuDst.Slice     (10, 10);
        ReadOnlyView<float> view2 = gpuSrc.AsReadOnly(20, 10);

        AddKernel(view1, view2);
        
        Device.Wait(gpuDst);
        gpuDst.Download(gpuDst, buffer1);
        
        // Device.Flush(true);
        // Device.Download();
        
        
        for (int n = 0; n < 10; n++) {
            Assert.That(view1.Span[n], Is.EqualTo(30 + 2 * n));
        }
    }
    
    // ----------------------------------------------
    [Kernel] [OmitHash]
    private static void Move([Span] ref float position, [Span] float velocity, float deltaTime) {
        position += velocity * deltaTime;
    }
    
    [Test]
    public void Test_Kernel_Fma()
    {
        for (int n = 0; n < 128; n++) {
            scalar1[n] = buffer1[n] = n;
            scalar2[n] = buffer2[n] = n + 100;
        }
        using var gpuBuffer1   = Device.CreateBuffer(buffer1, GpuBufferUsage.Storage | GpuBufferUsage.CopySrc, "position");
        using var gpuBuffer2   = Device.CreateBuffer(buffer2, GpuBufferUsage.Storage, "velocity");

        MoveVector(scalar1,          scalar2,       42, false);
        MoveKernel(gpuBuffer1.InOut, gpuBuffer2.In, 42);
        
        Device.Wait(gpuBuffer1);
        
        gpuBuffer1.Download(gpuBuffer1, buffer1);
        
        for (int n = 0; n < 128; n++) {
            Assert.That(scalar1[n], Is.EqualTo(buffer1[n]));
        }
    }
    
    // ----------------------------------------------
    [Kernel] [OmitHash]
    private static void UseConstant([Span] ref float position) {
        position += MathF.PI;
    }
    
    [Test]
    public void Test_Kernel_UseConstant()
    {
        for (int n = 0; n < 128; n++) {
            scalar1[n] = buffer1[n] = n;
            scalar2[n] = buffer2[n] = n + 100;
        }
        using var gpuBuffer1   = Device.CreateBuffer(buffer1, GpuBufferUsage.Storage | GpuBufferUsage.CopySrc, "position");

        UseConstantVector(scalar1, false);
        UseConstantKernel(gpuBuffer1.InOut);
        
        Device.Wait(gpuBuffer1);
        
        gpuBuffer1.Download(gpuBuffer1, buffer1);
        
        for (int n = 0; n < 128; n++) {
            Assert.That(scalar1[n], Is.EqualTo(buffer1[n]));
        }
    }

    // ----------------------------------------------
    [Kernel] [OmitHash]
    private static void InverseSqrt([Span] ref float position) {
        position = 5 / MathF.Sqrt(position);
    }
    
    [Test]
    public void Test_Kernel_InverseSqrt()
    {
        for (int n = 0; n < 128; n++) {
            scalar1[n] = buffer1[n] = n;
            scalar2[n] = buffer2[n] = n + 100;
        }
        using var gpuBuffer1   = Device.CreateBuffer(buffer1, GpuBufferUsage.Storage | GpuBufferUsage.CopySrc, "position");

        InverseSqrtVector(scalar1, false);
        InverseSqrtKernel(gpuBuffer1.InOut);
        
        Device.Wait(gpuBuffer1);
        
        gpuBuffer1.Download(gpuBuffer1, buffer1);
        
        for (int n = 0; n < 128; n++) {
            Assert.That(scalar1[n], Is.EqualTo(buffer1[n]).Within(1e-5f));
        }
    }
    
    // ----------------------------------------------
    [Kernel] [OmitHash]
    private static void Kernel_Trigonometry([Span]ref float position, [Span] float velocity, float value)
    {
        var fraction= velocity - MathF.Truncate(velocity);
        var gtOne   = value + MathF.Abs(velocity);
        var sin     = MathF.Sin(velocity);
        var cos     = MathF.Cos(velocity);
        var tan     = MathF.Tan(velocity);
        var asin    = MathF.Asin(fraction);
        var acos    = MathF.Acos(fraction);
        var atan    = MathF.Atan(velocity);
        var atan2   = MathF.Atan2(velocity, value);
        var asinh   = MathF.Asinh(velocity);
        var acosh   = MathF.Acosh(gtOne);
        var atanh   = MathF.Atanh(fraction);
        position += sin + cos + tan + asin + acos + atan + atan2 + asinh +  acosh + atanh;
    }
    
    [Test]
    public void Test_Kernel_Trigonometry()
    {
        for (int n = 0; n < 128; n++) {
            scalar1[n] = buffer1[n] = n;
            scalar2[n] = buffer2[n] = n + 100;
        }
        using var gpuBuffer1   = Device.CreateBuffer(buffer1, GpuBufferUsage.Storage | GpuBufferUsage.CopySrc, "position");
        using var gpuBuffer2   = Device.CreateBuffer(buffer2, GpuBufferUsage.Storage, "velocity");

        Kernel_TrigonometryVector(scalar1,    	    scalar2,       1.1f, false);
        Kernel_TrigonometryKernel(gpuBuffer1.InOut, gpuBuffer2.In, 1.1f);
        
        Device.Wait(gpuBuffer1);
        
        gpuBuffer1.Download(gpuBuffer1, buffer1);
        
        for (int n = 0; n < 128; n++) {
            Assert.That(scalar1[n], Is.EqualTo(buffer1[n]).Within(1e-2f));
            Assert.That(scalar1[n], Is.Not.NaN & Is.Not.EqualTo(float.PositiveInfinity) & Is.Not.EqualTo(float.NegativeInfinity));
        }
    }
    
    // ----------------------------------------------
    [Kernel] [OmitHash]
    private static void Kernel_Trigonometry2([Span]ref float position)
    {
        var sinh     = MathF.Sinh(position);
        var cosh     = MathF.Cosh(position);
        var tanh     = MathF.Tanh(position);
        position += sinh + cosh + tanh;
    }
    
    [Test]
    public void Test_Kernel_Trigonometry2()
    {
        for (int n = 0; n < 128; n++) {
            scalar1[n] = buffer1[n] = (n - 64f) / 64f * 10;
        }
        using var gpuBuffer1   = Device.CreateBuffer(buffer1, GpuBufferUsage.Storage | GpuBufferUsage.CopySrc, "position");
        using var gpuBuffer2   = Device.CreateBuffer(buffer2, GpuBufferUsage.Storage, "velocity");

        Kernel_Trigonometry2Vector(scalar1, false);
        Kernel_Trigonometry2Kernel(gpuBuffer1.InOut);
        
        Device.Wait(gpuBuffer1);
        
        gpuBuffer1.Download(gpuBuffer1, buffer1);
        
        for (int n = 0; n < 128; n++) {
            Assert.That(scalar1[n], Is.EqualTo(buffer1[n]).Within(0.01).Percent);
            Assert.That(scalar1[n], Is.Not.NaN & Is.Not.EqualTo(float.PositiveInfinity) & Is.Not.EqualTo(float.NegativeInfinity));
        }
    }
    
    // ----------------------------------------------
    [Kernel] [OmitHash]
    private static void Kernel_Misc([Span]ref float position, [Span] float velocity, float value)
    {
        var abs     = MathF.Abs(velocity);
        var sign    = MathF.Sign(velocity);
        var floor   = MathF.Floor(velocity);
        var ceiling = MathF.Ceiling(velocity);
        var log     = MathF.Log(value);
        var log2    = MathF.Log2(value);
        var log10   = MathF.Log10(abs);
        var exp     = MathF.Exp(velocity);
        var pow     = MathF.Pow(abs, velocity);
        var round   = MathF.Round(velocity);
        var sqrt    = MathF.Sqrt(abs);
        position = abs + sign + floor + ceiling + log + log2 + log10 + exp + pow + round + sqrt;
    }
    
    [Test]
    public void Test_Kernel_Misc()
    {
        for (int n = 0; n < 128; n++) {
            scalar1[n] = buffer1[n] = n * 0.123f;
            scalar2[n] = buffer2[n] = (n + 100) * 0.005f;
        }
        using var gpuBuffer1   = Device.CreateBuffer(buffer1, GpuBufferUsage.Storage | GpuBufferUsage.CopySrc, "position");
        using var gpuBuffer2   = Device.CreateBuffer(buffer2, GpuBufferUsage.Storage, "velocity");

        Kernel_MiscVector(scalar1,          scalar2,       1.1f, false);
        Kernel_MiscKernel(gpuBuffer1.InOut, gpuBuffer2.In, 1.1f);
        
        Device.Wait(gpuBuffer1);
        
        gpuBuffer1.Download(gpuBuffer1, buffer1);
        
        for (int n = 0; n < 128; n++) {
            Assert.That(scalar1[n], Is.EqualTo(buffer1[n]).Within(1e-5f));
            Assert.That(scalar1[n], Is.Not.NaN & Is.Not.EqualTo(float.PositiveInfinity) & Is.Not.EqualTo(float.NegativeInfinity));
        }
    }
    
    // ----------------------------------------------
    [Kernel] [OmitHash]
    private static void Kernel_Min([Span]ref float position, [Span] float velocity)
    {
        position = MathF.Min(position, velocity);
    }
    
    [Test]
    public void Test_Kernel_Min()
    {
        for (int n = 0; n < 128; n++) {
            scalar1[n] = buffer1[n] = n;
            scalar2[n] = buffer2[n] = n + 100;
        }
        using var gpuBuffer1   = Device.CreateBuffer(buffer1, GpuBufferUsage.Storage | GpuBufferUsage.CopySrc, "position");
        using var gpuBuffer2   = Device.CreateBuffer(buffer2, GpuBufferUsage.Storage, "velocity");

        Kernel_MinVector(scalar1,          scalar2, false);
        Kernel_MinKernel(gpuBuffer1.InOut, gpuBuffer2.In);
        
        Device.Wait(gpuBuffer1);
        
        gpuBuffer1.Download(gpuBuffer1, buffer1);
        
        for (int n = 0; n < 128; n++) {
            Assert.That(scalar1[n], Is.EqualTo(buffer1[n]).Within(1e-5f));
            Assert.That(scalar1[n], Is.Not.NaN & Is.Not.EqualTo(float.PositiveInfinity) & Is.Not.EqualTo(float.NegativeInfinity));
        }
    }
    
    // ----------------------------------------------
    [Kernel] [OmitHash]
    private static void Kernel_Max([Span]ref float position, [Span] float velocity)
    {
        position = MathF.Max(position, velocity);
    }
    
    [Test]
    public void Test_Kernel_Max()
    {
        for (int n = 0; n < 128; n++) {
            scalar1[n] = buffer1[n] = n;
            scalar2[n] = buffer2[n] = n + 100;
        }
        using var gpuBuffer1   = Device.CreateBuffer(buffer1, GpuBufferUsage.Storage | GpuBufferUsage.CopySrc, "position");
        using var gpuBuffer2   = Device.CreateBuffer(buffer2, GpuBufferUsage.Storage, "velocity");

        Kernel_MaxVector(scalar1,          scalar2, false);
        Kernel_MaxKernel(gpuBuffer1.InOut, gpuBuffer2.In);
        
        Device.Wait(gpuBuffer1);
        
        gpuBuffer1.Download(gpuBuffer1, buffer1);
        
        for (int n = 0; n < 128; n++) {
            Assert.That(scalar1[n], Is.EqualTo(buffer1[n]).Within(1e-5f));
            Assert.That(scalar1[n], Is.Not.NaN & Is.Not.EqualTo(float.PositiveInfinity) & Is.Not.EqualTo(float.NegativeInfinity));
        }
    }
    
    // ----------------------------------------------
    [Kernel] [OmitHash]
    private static void Kernel_Clamp([Span]ref float position, [Span] float min, float max)
    {
        position = Math.Clamp(position, min, max);
    }
    
    [Test]
    public void Test_Kernel_Clamp()
    {
        for (int n = 0; n < 128; n++) {
            scalar1[n] = buffer1[n] = n + 100;
            scalar2[n] = buffer2[n] = n;
        }
        using var gpuBuffer1   = Device.CreateBuffer(buffer1, GpuBufferUsage.Storage | GpuBufferUsage.CopySrc, "position");
        using var gpuBuffer2   = Device.CreateBuffer(buffer2, GpuBufferUsage.Storage, "velocity");

        Kernel_ClampVector(scalar1,          scalar2,       200, false);
        Kernel_ClampKernel(gpuBuffer1.InOut, gpuBuffer2.In, 200);
        
        Device.Wait(gpuBuffer1);
        
        gpuBuffer1.Download(gpuBuffer1, buffer1);
        
        for (int n = 0; n < 128; n++) {
            Assert.That(scalar1[n], Is.EqualTo(buffer1[n]).Within(1e-5f));
            Assert.That(scalar1[n], Is.Not.NaN & Is.Not.EqualTo(float.PositiveInfinity) & Is.Not.EqualTo(float.NegativeInfinity));
        }
    }
}
