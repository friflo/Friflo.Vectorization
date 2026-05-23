
using Friflo.Vectorization.GPU;
using Tests.Generators;
using Kernel;


/// <summary>
/// Used to check GPU handle counts within a debugger from any place using:<br/>
/// <c>Dbg.HandleDiff</c>. 
/// </summary>
internal static class Dbg
{
    internal static KernelBase      Instance;
    internal static GpuHandleDiff   HandleDiff => Instance.HandleDiff;
}