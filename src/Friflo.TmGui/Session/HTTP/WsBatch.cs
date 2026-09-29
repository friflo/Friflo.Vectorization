// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToPrimaryConstructor
namespace Friflo.TmGui.Session.HTTP;


internal class WsBatch : TmBatch
{
    public WsBatch(TmGuiBackend backend, int maxVertices) : base(backend, maxVertices)
    {
    }

    protected internal override void InitBatch()
    {
        
    }
}