// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Numerics;
using Friflo.Vectorization;
using Friflo.Vectorization.GPU;
using NUnit.Framework;

// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Kernel.Generators;


public partial class Test_Vector2_GPU : KernelBase
{
    private readonly Vector2[]    array1  = new Vector2[128];
    private readonly Vector2[]    array2  = new Vector2[128];
    private readonly Vector2[]    buffer1 = new Vector2[128];
    private readonly Vector2[]    buffer2 = new Vector2[128];

    // ----------------------------------------------
    [Kernel] [OmitHash]
    private static void Multiply([Span] ref Vector2 position, [Span] Vector2 velocity) {
        position *= velocity;
    }
        
    [Test]
    public void Test_Kernel_Multiply()
    {
        for (int n = 0; n < 128; n++) {
            array1[n] = buffer1[n] = new Vector2(n+1,n+1);
            array2[n] = buffer2[n] = new Vector2(n+100,n+100);
        }
        using var gpuBuffer1   = Device.CreateBuffer(buffer1, "position", BufferProfile.InOut);
        using var gpuBuffer2   = Device.CreateBuffer(buffer2, "velocity", BufferProfile.StaticIn);        

        MultiplyVector(array1,           array2, false);
        MultiplyKernel(gpuBuffer1.InOut, gpuBuffer2.In);
        
        Device.Download();
        
        for (int n = 0; n < 128; n++) {
            Assert.That(array1[n], Is.EqualTo(buffer1[n]));
        }
    }
    
    // ----------------------------------------------
    [Kernel] [OmitHash]
    private static void Arithmetic([Span] ref Vector2 position, [Span] Vector2 velocity) {
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
            array1[n] = buffer1[n] = new Vector2(n,n);
            array2[n] = buffer2[n] = new Vector2(n+100,n+100);
        }
        using var gpuBuffer1   = Device.CreateBuffer(buffer1, "position", BufferProfile.InOut);
        using var gpuBuffer2   = Device.CreateBuffer(buffer2, "velocity", BufferProfile.StaticIn);        

        ArithmeticVector(array1,           array2, false);
        ArithmeticKernel(gpuBuffer1.InOut, gpuBuffer2.In);
        
        Device.Download();
        
        for (int n = 0; n < 128; n++) {
            Assert.That(array1[n], Is.EqualTo(buffer1[n]));
        }
    }
    
    // ----------------------------------------------
    [Kernel] [OmitHash]
    private static void Misc([Span] ref Vector2 position, [Span] Vector2 velocity, Vector2 max) {
        var abs     = Vector2.Abs(velocity);
        var trunc   = Vector2.Truncate(velocity);
        var round   = Vector2.Round(velocity);
        var min     = Vector2.Min(position, velocity);
        var max2    = Vector2.Max(position, velocity);
        var clamp   = Vector2.Clamp(position, velocity, max);
        var lerp    = Vector2.Lerp(position, velocity, max);
        position    = abs + trunc + round + min + max2 + clamp + lerp;
    }
        
    [Test]
    public void Test_Kernel_Misc()
    {
        for (int n = 0; n < 128; n++) {
            array1[n] = buffer1[n] = new Vector2(n * 0.1f,       n * 0.1f);
            array2[n] = buffer2[n] = new Vector2(n * 0.1f + 100, n * 0.1f + 100);
        }
        using var gpuBuffer1   = Device.CreateBuffer(buffer1, "position", BufferProfile.InOut);
        using var gpuBuffer2   = Device.CreateBuffer(buffer2, "velocity", BufferProfile.StaticIn);        

        MiscVector(array1,           array2,        new Vector2(5.5f, 6.6f), false);
        MiscKernel(gpuBuffer1.InOut, gpuBuffer2.In, new Vector2(5.5f, 6.6f));
        
        Device.Download();
        
        for (int n = 0; n < 128; n++) {
            var a = array1[n];
            var b = buffer1[n];
            Assert.That(a.X, Is.EqualTo(b.X).Within(1e-3f));
            Assert.That(a.Y, Is.EqualTo(b.Y).Within(1e-3f));
        }
    }
    
    // ----------------------------------------------
    [Kernel] [OmitHash]
    private static void Advanced([Span] ref Vector2 position, [Span] Vector2 velocity) {
        float   cross       = Vector2.Cross(position, velocity);
        var     normalize   = Vector2.Normalize(velocity);
        float   length      = position.Length();
        float   dist        = Vector2.Distance(position, velocity);
        float   distSquared = Vector2.DistanceSquared(position, velocity);
        float   sum = cross + length + dist + distSquared;
        position = sum * normalize;
    }
        
    [Test]
    public void Test_Kernel_Advanced()
    {
        for (int n = 0; n < 128; n++) {
            array1[n] = buffer1[n] = new Vector2(n * 0.1f,       n * 0.1f);
            array2[n] = buffer2[n] = new Vector2(n * 0.1f + 100, n * 0.1f + 100);
        }
        using var gpuBuffer1   = Device.CreateBuffer(buffer1, "position", BufferProfile.InOut);
        using var gpuBuffer2   = Device.CreateBuffer(buffer2, "velocity", BufferProfile.StaticIn);        

        AdvancedVector(array1,           array2, false);
        AdvancedKernel(gpuBuffer1.InOut, gpuBuffer2.In);
        
        Device.Download();
        
        for (int n = 0; n < 128; n++) {
            var a = array1[n];
            var b = buffer1[n];
            Assert.That(a.X, Is.EqualTo(b.X).Within(1e-2f));
            Assert.That(a.Y, Is.EqualTo(b.Y).Within(1e-2f));
        }
    }
    
    // ----------------------------------------------
    [Kernel] [OmitHash]
    private static void Transform([Span] ref Vector2 position, Matrix4x4 matrix) {
        position = Vector2.Transform(position, matrix);
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
            array1[n] = buffer1[n] = new Vector2(n * 0.1f,       n * 0.1f);
        }
        using var gpuBuffer1   = Device.CreateBuffer(buffer1, "position", BufferProfile.InOut);
        using var gpuBuffer2   = Device.CreateBuffer(buffer2, "velocity", BufferProfile.StaticIn);        

        TransformVector(array1,           matrix, false);
        TransformKernel(gpuBuffer1.InOut, matrix);
        
        Device.Download();
        
        for (int n = 0; n < 128; n++) {
            var a = array1[n];
            var b = buffer1[n];
            Assert.That(a.X, Is.EqualTo(b.X).Within(1e-6f));
            Assert.That(a.Y, Is.EqualTo(b.Y).Within(1e-6f));
        }
    }
}
