// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using Friflo.Vectorization;
using Friflo.Vectorization.GPU;
using NUnit.Framework;
using Tests.GPU;

// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Tests.Generators.Kernel;


public partial class Test_Float_GPU : GpuTestBase
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

        MultiplyVector(scalar1,    scalar2, false);
        MultiplyKernel(gpuBuffer1, gpuBuffer2);
        
        Device.Wait(gpuBuffer1);
        
        gpuBuffer1.Download(gpuBuffer1, buffer1);
        
        for (int n = 0; n < 128; n++) {
            Assert.That(scalar1[n], Is.EqualTo(buffer1[n]));
        }
        MultiplyKernel(gpuBuffer1, gpuBuffer2);
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
        using var gpuBuffer1   = Device.CreateBuffer(buffer1, GpuBufferUsage.Storage | GpuBufferUsage.CopySrc, "position");
        using var gpuBuffer2   = Device.CreateBuffer(buffer2, GpuBufferUsage.Storage, "velocity");

        AssignVector(scalar1,    scalar2, false);
        AssignKernel(gpuBuffer1, gpuBuffer2);
        
        Device.Wait(gpuBuffer1);
        
        gpuBuffer1.Download(gpuBuffer1, buffer1);
        
        for (int n = 0; n < 128; n++) {
            Assert.That(scalar1[n], Is.EqualTo(buffer1[n]));
        }
        MultiplyKernel(gpuBuffer1, gpuBuffer2);
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

        MoveVector(scalar1, scalar2, 42, false);
        MoveKernel(gpuBuffer1, gpuBuffer2, 42);
        
        Device.Wait(gpuBuffer1);
        
        gpuBuffer1.Download(gpuBuffer1, buffer1);
        
        for (int n = 0; n < 128; n++) {
            Assert.That(scalar1[n], Is.EqualTo(buffer1[n]));
        }
        MultiplyKernel(gpuBuffer1, gpuBuffer2);
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
        using var gpuBuffer2   = Device.CreateBuffer(buffer2, GpuBufferUsage.Storage, "velocity");

        UseConstantVector(scalar1, false);
        UseConstantKernel(gpuBuffer1);
        
        Device.Wait(gpuBuffer1);
        
        gpuBuffer1.Download(gpuBuffer1, buffer1);
        
        for (int n = 0; n < 128; n++) {
            Assert.That(scalar1[n], Is.EqualTo(buffer1[n]));
        }
        MultiplyKernel(gpuBuffer1, gpuBuffer2);
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
        using var gpuBuffer2   = Device.CreateBuffer(buffer2, GpuBufferUsage.Storage, "velocity");

        InverseSqrtVector(scalar1, false);
        InverseSqrtKernel(gpuBuffer1);
        
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
        position += sin + cos + tan + asin + acos + atan + atan2 + asinh + acosh + atanh;
    }
    
    // [Test]
    public void Test_Kernel_Trigonometry()
    {
        for (int n = 0; n < 128; n++) {
            scalar1[n] = buffer1[n] = n;
            scalar2[n] = buffer2[n] = n + 100;
        }
        using var gpuBuffer1   = Device.CreateBuffer(buffer1, GpuBufferUsage.Storage | GpuBufferUsage.CopySrc, "position");
        using var gpuBuffer2   = Device.CreateBuffer(buffer2, GpuBufferUsage.Storage, "velocity");

        Kernel_TrigonometryVector(scalar1, scalar2, 1.1f, false);
        Kernel_TrigonometryKernel(scalar1, scalar2, 1.1f);
        
        Device.Wait(gpuBuffer1);
        
        gpuBuffer1.Download(gpuBuffer1, buffer1);
        
        for (int n = 0; n < 128; n++) {
            Assert.That(scalar1[n], Is.EqualTo(buffer1[n]).Within(1e-5f));
        }
    }

}
