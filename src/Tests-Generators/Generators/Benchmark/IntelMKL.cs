using System;
using System.Runtime.InteropServices;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

// ReSharper disable InconsistentNaming
namespace Bench;



public static class IntelMKL
{
    [DllImport("mkl_rt.3.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern unsafe void cblas_saxpy(
        int n,           // Number of elements
        float a,         // Scalar multiplier (deltaTime)
        float* x,        // Source array (velocity)
        int incx,        // Stride for x (usually 1)
        float* y,        // Destination array (position)
        int incy         // Stride for y (usually 1)
    );
}


[AttributeUsage(AttributeTargets.Method)]
public class MKLAttribute : NUnitAttribute, IApplyToTest
{
    public void ApplyToTest(Test test)
    {
#if !MKL
        test.RunState = RunState.Ignored;
        test.Properties.Set(PropertyNames.SkipReason, "MKL disabled.");
        // To enable MKL Tests add  <DefineConstants>MKL</DefineConstants> to *.csproj
#endif
    }
}

