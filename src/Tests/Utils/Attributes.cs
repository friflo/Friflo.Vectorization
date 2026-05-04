using System;
using System.Runtime.Intrinsics.X86;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace Tests.Utils {

[AttributeUsage(AttributeTargets.Method)]
public class AvxOnlyAttribute : NUnitAttribute, IApplyToTest
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

}