
using Tests.Generators.Lab;

internal static class Dbg
{
    internal static GpuTestBase  Instance;
    internal static GpuHandles   HandleDiff => Instance.HandleDiff;
}