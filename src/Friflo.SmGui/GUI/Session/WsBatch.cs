// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace

using System;
using System.Text;

namespace Friflo.SmGui.Session;


internal class WsBatch : SmBatch
{
    internal WsBatch(SessionId sessionId, SmGuiBackend backend, int maxVertices, LoopTimer timer)
        : base(sessionId, backend, maxVertices, timer)
    {
    }

    protected internal override void InitBatch() { }
    
    internal void DrawCommandList()
    {
        EndBatch();
    }
}
