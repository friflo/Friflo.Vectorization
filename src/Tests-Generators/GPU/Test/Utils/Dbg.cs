
using Friflo.Vectorization.GPU;
using Tests.GPU;

/// <summary>
/// Used to check GPU handle counts within a debugger from any place using:<br/>
/// <c>Dbg.HandleDiff</c>. 
/// </summary>
internal static class Dbg
{
    internal static GpuTestBase  Instance;
    internal static GpuHandles   HandleDiff => Instance.HandleDiff;
}