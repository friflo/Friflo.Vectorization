// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Numerics;
using Friflo.Vectorization;
using Friflo.Vectorization.GPU;
using NUnit.Framework;
using Tests.GPU;

// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Tests.Generators.Kernel;


public partial class Test_Vector4_GPU : GpuTestBase
{
    private readonly Vector4[]    array1  = new Vector4[128];
    private readonly Vector4[]    array2  = new Vector4[128];
    private readonly Vector4[]    buffer1 = new Vector4[128];
    private readonly Vector4[]    buffer2 = new Vector4[128];

    // ----------------------------------------------
    [Kernel] [OmitHash]
    private static void Multiply([Span] ref Vector4 position, [Span] Vector4 velocity) {
        position *= velocity;
    }
        
    [Test]
    public void Test_Kernel_Multiply()
    {
        for (int n = 0; n < 128; n++) {
            array1[n] = buffer1[n] = new Vector4(n+1,n+1,n+1,n+1);
            array2[n] = buffer2[n] = new Vector4(n+100,n+100, n+100,n+100);
        }
        using var gpuBuffer1   = Device.CreateBuffer(buffer1, GpuBufferUsage.Storage | GpuBufferUsage.CopySrc, "position");
        using var gpuBuffer2   = Device.CreateBuffer(buffer2, GpuBufferUsage.Storage, "velocity");        

        MultiplyVector(array1,           array2, false);
        MultiplyKernel(gpuBuffer1.InOut, gpuBuffer2.In);
        
        Device.Wait(gpuBuffer1);
        
        gpuBuffer1.Download(gpuBuffer1, buffer1);
        
        for (int n = 0; n < 128; n++) {
            Assert.That(array1[n], Is.EqualTo(buffer1[n]));
        }
    }

    // ----------------------------------------------
    [Kernel] [OmitHash]
    private static void Arithmetic([Span] ref Vector4 position, [Span] Vector4 velocity) {
        var add = position + velocity;
        var sub = position - velocity;
        var mul = position * velocity;
        var div = position / velocity;
        position += add;
        position -= sub;
        position += mul;
        position -= div;
    }
        
    [Test]
    public void Test_Kernel_Arithmetic()
    {
        for (int n = 0; n < 128; n++) {
            array1[n] = buffer1[n] = new Vector4(n,n,n,n);
            array2[n] = buffer2[n] = new Vector4(n+100,n+100,n+100,n+100);
        }
        using var gpuBuffer1   = Device.CreateBuffer(buffer1, GpuBufferUsage.Storage | GpuBufferUsage.CopySrc, "position");
        using var gpuBuffer2   = Device.CreateBuffer(buffer2, GpuBufferUsage.Storage, "velocity");        

        ArithmeticVector(array1,           array2, false);
        ArithmeticKernel(gpuBuffer1.InOut, gpuBuffer2.In);
        
        Device.Wait(gpuBuffer1);
        
        gpuBuffer1.Download(gpuBuffer1, buffer1);
        
        for (int n = 0; n < 128; n++) {
            Assert.That(array1[n], Is.EqualTo(buffer1[n]));
        }
    }

    // ----------------------------------------------
    [Kernel] [OmitHash]
    private static void Misc([Span] ref Vector4 position, [Span] Vector4 velocity, Vector4 max) {
        var abs     = Vector4.Abs(velocity);
        var trunc   = Vector4.Truncate(velocity);
        var round   = Vector4.Round(velocity);
        var min     = Vector4.Min(position, velocity);
        var max2    = Vector4.Max(position, velocity);
        var clamp   = Vector4.Clamp(position, velocity, max);
        var lerp    = Vector4.Lerp(position, velocity, max);
        position    = abs + trunc + round + min + max2 + clamp + lerp;
    }
        
    [Test]
    public void Test_Kernel_Misc()
    {
        for (int n = 0; n < 128; n++) {
            array1[n] = buffer1[n] = new Vector4(n * 0.1f,       n * 0.1f,       n * 0.1f,       n * 0.1f);
            array2[n] = buffer2[n] = new Vector4(n * 0.1f + 100, n * 0.1f + 100, n * 0.1f + 100, n * 0.1f + 100);
        }
        using var gpuBuffer1   = Device.CreateBuffer(buffer1, GpuBufferUsage.Storage | GpuBufferUsage.CopySrc, "position");
        using var gpuBuffer2   = Device.CreateBuffer(buffer2, GpuBufferUsage.Storage, "velocity");        

        MiscVector(array1,     		 array2,        new Vector4(5.5f, 6.6f, 7.7f, 8.8f), false);
        MiscKernel(gpuBuffer1.InOut, gpuBuffer2.In, new Vector4(5.5f, 6.6f, 7.7f, 8.8f));
        
        Device.Wait(gpuBuffer1);
        
        gpuBuffer1.Download(gpuBuffer1, buffer1);
        
        for (int n = 0; n < 128; n++) {
            var a = array1[n];
            var b = buffer1[n];
            Assert.That(a.X, Is.EqualTo(b.X).Within(1e-3f));
            Assert.That(a.Y, Is.EqualTo(b.Y).Within(1e-3f));
            Assert.That(a.Z, Is.EqualTo(b.Z).Within(1e-3f));
            Assert.That(a.W, Is.EqualTo(b.W).Within(1e-3f));
        }
    }
    /*
    // ----------------------------------------------
    [Kernel] [OmitHash]
    private static void Advanced([Span] ref Vector4 position, [Span] Vector4 velocity) {
        var     cross       = Vector4.Cross(position, velocity);
        var     normalize   = Vector4.Normalize(velocity);
        float   length      = position.Length();
        float   dist        = Vector4.Distance(position, velocity);
        float   distSquared = Vector4.DistanceSquared(position, velocity);
        float   sum = length + dist + distSquared;
        position = cross + sum * normalize;
    }
        
    [Test]
    public void Test_Kernel_Advanced()
    {
        for (int n = 0; n < 128; n++) {
            array1[n] = buffer1[n] = new Vector4(n * 0.1f,       n * 0.1f,       n * 0.1f,       n * 0.1f);
            array2[n] = buffer2[n] = new Vector4(n * 0.1f + 100, n * 0.1f + 100, n * 0.1f + 100, n * 0.1f + 100);
        }
        using var gpuBuffer1   = Device.CreateBuffer(buffer1, GpuBufferUsage.Storage | GpuBufferUsage.CopySrc, "position");
        using var gpuBuffer2   = Device.CreateBuffer(buffer2, GpuBufferUsage.Storage, "velocity");        

        AdvancedVector(array1,     array2, false);
        AdvancedKernel(gpuBuffer1, gpuBuffer2);
        
        Device.Wait(gpuBuffer1);
        
        gpuBuffer1.Download(gpuBuffer1, buffer1);
        
        for (int n = 0; n < 128; n++) {
            var a = array1[n];
            var b = buffer1[n];
            Assert.That(a.X, Is.EqualTo(b.X).Within(1e-2f));
            Assert.That(a.Y, Is.EqualTo(b.Y).Within(1e-2f));
            Assert.That(a.Z, Is.EqualTo(b.Z).Within(1e-2f));
            Assert.That(a.W, Is.EqualTo(b.W).Within(1e-2f));
        }
    } */
    
    // ----------------------------------------------
    [Kernel] [OmitHash]
    private static void Transform([Span] ref Vector4 position, Matrix4x4 matrix) {
        position = Vector4.Transform(position, matrix);
    }
        
    [Test]
    public void Test_Kernel_Transform()
    {
        Matrix4x4 rot = Matrix4x4.CreateFromYawPitchRoll(
            10f * (MathF.PI / 180.0f), // Yaw
            20f * (MathF.PI / 180.0f), // Pitch
            30f * (MathF.PI / 180.0f)  // Roll
        );
        Matrix4x4 trans = Matrix4x4.CreateTranslation(new Vector3(1f, 2f, 3f));
        var matrix = Matrix4x4.Multiply(rot, trans);
        
        for (int n = 0; n < 128; n++) {
            array1[n] = buffer1[n] = new Vector4(n * 0.1f, n * 0.1f, n * 0.1f, n * 0.1f);
        }
        using var gpuBuffer1   = Device.CreateBuffer(buffer1, GpuBufferUsage.Storage | GpuBufferUsage.CopySrc, "position");
        using var gpuBuffer2   = Device.CreateBuffer(buffer2, GpuBufferUsage.Storage, "velocity");        

        TransformVector(array1,           matrix, false);
        TransformKernel(gpuBuffer1.InOut, matrix);
        
        Device.Wait(gpuBuffer1);
        
        gpuBuffer1.Download(gpuBuffer1, buffer1);
        
        for (int n = 0; n < 128; n++) {
            var a = array1[n];
            var b = buffer1[n];
            Assert.That(a.X, Is.EqualTo(b.X).Within(1e-5f));
            Assert.That(a.Y, Is.EqualTo(b.Y).Within(1e-5f));
            Assert.That(a.Z, Is.EqualTo(b.Z).Within(1e-5f));
            Assert.That(a.W, Is.EqualTo(b.W).Within(1e-5f));
        }
    }
}
