// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Http;


internal class WsBatch : TmBatch
{
    public WsBatch(TmGuiBackend backend, int maxVertices) : base(backend, maxVertices)
    {
    }

    protected override void InitBatch()
    {
        
    }
    
    internal void DrawCommandList()
    {
        EndBatch();
    }
}