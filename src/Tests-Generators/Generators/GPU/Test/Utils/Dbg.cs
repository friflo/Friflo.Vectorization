
using Tests.Generators.GPU;

internal static class Dbg
{
    internal static GpuTestBase  Instance;
    internal static GpuHandles   HandleDiff => Instance.Handles;
}