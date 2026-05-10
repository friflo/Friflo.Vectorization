
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU;

public abstract class GpuAdapter
{
    public abstract bool        IsDisposed { get;  }
    
    public abstract GpuDevice   CreateDevice (string label, int maxTasks = 64, int slotSize = 64 * 1024);
    public abstract GpuHandles  GenerateHandles ();
}