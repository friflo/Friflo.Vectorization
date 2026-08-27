// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Numerics;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.ImGui.Headless;

public sealed class HeadlessBatch : ImBatch
{
    internal HeadlessBatch(HeadlessBackend backend, int maxVertices)
        : base(backend, maxVertices)
    {
    }
    
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