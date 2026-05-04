using System;
using System.Runtime.Intrinsics.X86;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace Tests.Generators.Lab;

[AttributeUsage(AttributeTargets.Method)]
public class AvxAttribute : NUnitAttribute, IApplyToTest
{
    public void ApplyToTest(Test test)
    {
        if (!Avx.IsSupported)
        {
            test.RunState = RunState.Skipped;
            test.Properties.Set(PropertyNames.SkipReason, "CPU no support of: AVX");
        }
    }
}