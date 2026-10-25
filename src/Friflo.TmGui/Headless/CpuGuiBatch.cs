// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Numerics;
using Friflo.SmGui.Session;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.Headless;

public sealed class CpuGuiBatch : SmBatch
{
    internal CpuGuiBatch(SessionId sessionId, CpuBackend backend, int maxVertices, LoopTimer? timer)
        : base(sessionId, backend, maxVertices, timer)
    {
    }
    
    protected internal override void InitBatch() { }
    
    public void DrawCommandList()
    {
        EndBatch();
        
        var scissor = new RectVector2(Vector2.Zero, viewport);

        foreach (var cmd in DrawList)
        {
            if (!cmd.scissor.Equals(scissor)) {
                scissor = cmd.scissor;
                // <set scissor call>    
            }
            // <draw command call>
        }
    }
}