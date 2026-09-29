namespace Friflo.TmGui.Session.HTTP;

internal class WsBatch : TmBatch
{
    public WsBatch(TmGuiBackend backend) : base(backend)
    {
    }

    public WsBatch(TmGuiBackend backend, int maxVertices) : base(backend, maxVertices)
    {
    }

    protected internal override void InitBatch()
    {
        
    }
}