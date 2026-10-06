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
        Span<VertexQuad> newQuads = MemoryMarshal.Cast<Vertex2D, VertexQuad>(vertices);
        var newHashes = SequenceDiff.GetSpanOf(ref shared.quadHashesBuffer, newQuads.Length);
        
        for (int n= 0; n < newQuads.Length; n++) {
            newHashes[n] = newQuads[n].GetHashCode();
        }
        var clientQuads = CollectionsMarshal.AsSpan(clientQuadList);
        const int lookahead = 64;
        if (!SequenceDiff.TryComputeChanges(clientQuads, newHashes, 100000, lookahead, shared.changeList, out int diffValueCount)) {
            Debug.Fail("TryComputeChanges is false");
        }

        DebugVerifyHashDiff(clientQuads, newHashes, diffValueCount);
        
        if (shared.changeList.Count == 0) {
            return vertices;
        }
        
        clientQuadList.Clear();
        clientQuadList.AddRange(newHashes);
        
        return vertices;
        
        var diffQuads = SequenceDiff.FillDiffValues(shared.changeList, newQuads, ref shared.quadBuffer, diffValueCount);
        
        DebugVerifyQuadDiff(newQuads, diffQuads);
        
        return vertices;
    }


#region Debug - Verify
    private             int[]               debugHashDiff       = [];
    private             int[]               debugHashTarget     = [];
    
    private  readonly   List<VertexQuad>    debugClientQuads    = [];
    private             VertexQuad[]        debugClientTarget   = [];
    
    private void DebugVerifyHashDiff(ReadOnlySpan<int> clientQuads, ReadOnlySpan<int> newQuads, int diffValueCount)
    {
        if (shared.changeList.Count == 0) return;
        
        var diffQuads = SequenceDiff.FillDiffValues(shared.changeList, newQuads, ref debugHashDiff, diffValueCount);
        
        var clientTarget = SequenceDiff.ApplyChanges(clientQuads, shared.changeList, diffQuads, ref debugHashTarget, newQuads.Length);

        var isEqual = newQuads.SequenceEqual(clientTarget);
        if (!isEqual) {
            int n = 0;
            for (; n < newQuads.Length; n++) {
                if (newQuads[n] != clientTarget[n]) {
                    break;
                }
            }
            Debug.Fail($"DebugVerifyHashDiff failed. Diff at: {n}");
        }
    }
    
    private void DebugVerifyQuadDiff(ReadOnlySpan<VertexQuad> newQuads, ReadOnlySpan<VertexQuad> diffQuads)
    {
        if (shared.changeList.Count == 0) return;
        
        ReadOnlySpan<VertexQuad> clientQuads = CollectionsMarshal.AsSpan(debugClientQuads);
        
        var clientTarget = SequenceDiff.ApplyChanges(clientQuads, shared.changeList, diffQuads, ref debugClientTarget, newQuads.Length);

        var isEqual = newQuads.SequenceEqual(clientTarget);
        if (!isEqual) {
            int n = 0;
            for (; n < newQuads.Length; n++) {
                if (newQuads[n] != clientTarget[n]) {
                    break;
                }
            }
            Debug.Fail($"DebugVerifyQuadDiff failed. Diff at: {n}");    
        }
        debugClientQuads.Clear();
        debugClientQuads.AddRange(newQuads);
    }
#endregion

}

