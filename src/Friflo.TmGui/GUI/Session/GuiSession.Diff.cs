// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable InlineTemporaryVariable
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Session;


internal sealed partial class GuiSession
{
    private Span<Vertex2D> CalcQuadChanges(Span<Vertex2D> vertices)
    {
        Span<VertexQuad> quads = MemoryMarshal.Cast<Vertex2D, VertexQuad>(vertices);
        var newHashes = SequenceDiff.GetSpanOf(ref shared.quadHashesBuffer, quads.Length);
        
        for (int n= 0; n < quads.Length; n++) {
            newHashes[n] = quads[n].GetHashCode();
        }
        var clientQuads = CollectionsMarshal.AsSpan(clientQuadList);
        const int lookahead = 64;
        if (!SequenceDiff.TryComputeChanges(clientQuads, newHashes, 100000, lookahead, shared.changeList, out int diffValueCount)) {
            Debug.Fail("TryComputeChanges is false");
        }

        DebugVerifyHashDiff(clientQuads, newHashes, diffValueCount);
            
        clientQuadList.Clear();
        clientQuadList.AddRange(newHashes);
        return vertices;
        if (shared.changeList.Count == 0) {
            return vertices;
        }
        
        var diffQuads = SequenceDiff.FillDiffValues(shared.changeList, quads, ref shared.quadBuffer, diffValueCount);
        
        DebugVerifyQuadDiff(quads, diffQuads);
        
        return vertices;
    }


#region Debug - Verify
    private             int[]               debugHashDiff       = [];
    private             int[]               debugHashTarget     = [];
    
    private  readonly   List<VertexQuad>    debugClientQuads    = [];
    private             VertexQuad[]        debugClientTarget   = [];
    
    private void DebugVerifyHashDiff(ReadOnlySpan<int> clientQuads, ReadOnlySpan<int> quads, int diffValueCount)
    {
        if (shared.changeList.Count == 0) return;
        
        var diffQuads = SequenceDiff.FillDiffValues(shared.changeList, quads, ref debugHashDiff, diffValueCount);
        
        var hashTarget = SequenceDiff.ApplyChanges(clientQuads, shared.changeList, diffQuads, ref debugHashTarget, quads.Length);

        var isEqual = quads.SequenceEqual(hashTarget);
        if (!isEqual) {
            int n = 0;
            for (; n < quads.Length; n++) {
                if (quads[n] != hashTarget[n]) {
                    break;
                }
            }
            Debug.Fail($"DebugVerifyHashDiff failed. Diff at: {n}");
        }
    }
    
    private void DebugVerifyQuadDiff(ReadOnlySpan<VertexQuad> quads, ReadOnlySpan<VertexQuad> diffQuads)
    {
        if (shared.changeList.Count == 0) return;
        
        ReadOnlySpan<VertexQuad> clientQuads = CollectionsMarshal.AsSpan(debugClientQuads);
        
        var clientTarget = SequenceDiff.ApplyChanges(clientQuads, shared.changeList, diffQuads, ref debugClientTarget, quads.Length);

        var isEqual = quads.SequenceEqual(clientTarget);
        if (!isEqual) {
            int n = 0;
            for (; n < quads.Length; n++) {
                if (quads[n] != clientTarget[n]) {
                    break;
                }
            }
            Debug.Fail($"DebugVerifyQuadDiff failed. Diff at: {n}");    
        }
        debugClientQuads.Clear();
        debugClientQuads.AddRange(quads);
    }
#endregion

}

