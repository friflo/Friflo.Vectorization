// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

// ReSharper disable RedundantAssignment
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable InlineTemporaryVariable
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Session;


internal sealed partial class GuiSession
{
    private Span<Vertex2D> CalcVerticesDiff(Span<Vertex2D> vertices)
    {
        Span<VertexQuad> newQuads = MemoryMarshal.Cast<Vertex2D, VertexQuad>(vertices);
        var newHashes = SequenceDiff.GetSpanOf(ref shared.quadHashesBuffer, newQuads.Length);
        
        for (int n= 0; n < newQuads.Length; n++) {
            newHashes[n] = newQuads[n].GetQuadHash();
        }
        var clientQuads = CollectionsMarshal.AsSpan(clientQuadList);
        const int lookahead = 64;
        var changeList = shared.changeList;
        if (!SequenceDiff.TryComputeChanges(clientQuads, newHashes, int.MaxValue, lookahead, changeList, out int diffValueCount)) {
            Debug.Fail("TryComputeChanges is false");
        }

        if (changeList.Count == 0) {
            return default;
        }
        DebugVerifyHashDiff(clientQuads, newHashes, diffValueCount);

        clientQuadList.Clear();
        clientQuadList.AddRange(newHashes);
        
        var diffQuads = SequenceDiff.FillDiffValues(changeList, newQuads, ref shared.quadBuffer, diffValueCount);
        
        DebugVerifyQuadDiff(newQuads, diffQuads);
        
        return MemoryMarshal.Cast<VertexQuad, Vertex2D>(diffQuads);
    }


#region Debug - Verify
    private             int[]               debugHashDiff       = [];
    private             int[]               debugHashTarget     = [];
    
    private  readonly   List<VertexQuad>    debugClientQuads    = [];
    private             VertexQuad[]        debugClientTarget   = [];
    
    [Conditional("DEBUG")]
    private void DebugVerifyHashDiff(ReadOnlySpan<int> clientQuads, ReadOnlySpan<int> newQuads, int diffValueCount)
    {
        var changeList = shared.changeList;
        
        var diffQuads = SequenceDiff.FillDiffValues(changeList, newQuads, ref debugHashDiff, diffValueCount);
        
        var clientTarget = SequenceDiff.ApplyChanges(clientQuads, changeList, diffQuads, ref debugHashTarget, newQuads.Length);

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
    
    [Conditional("DEBUG")]
    private void DebugVerifyQuadDiff(ReadOnlySpan<VertexQuad> newQuads, ReadOnlySpan<VertexQuad> diffQuads)
    {
        ReadOnlySpan<VertexQuad> clientQuads = CollectionsMarshal.AsSpan(debugClientQuads);
        
        ReadOnlySpan<VertexQuad> clientTarget = SequenceDiff.ApplyChanges(clientQuads, shared.changeList, diffQuads, ref debugClientTarget, newQuads.Length);

        var isEqual = newQuads.SequenceEqual(clientTarget);
        if (!isEqual) {
            int hashCollisionCount = 0;
            int n = 0;
            for (; n < newQuads.Length; n++) {
                ref readonly var newQuad     = ref newQuads[n];
                ref readonly var clientQuad  = ref clientTarget[n];
                // Only check when there is an actual content mismatch
                if (newQuad != clientQuad) {
                    if (newQuad.GetQuadHash() == clientQuad.GetQuadHash()) {
                        hashCollisionCount++;
                        continue;
                    }
                    Debug.Fail($"DebugVerifyQuadDiff failed. Diff at: {n}");
                    break; // Break execution on the first mismatch
                }
            }
            if (hashCollisionCount > 0) {
                Debug.WriteLine($"DebugVerifyQuadDiff - hashCollisionCount: {hashCollisionCount}");
            }
        }
        debugClientQuads.Clear();
        debugClientQuads.AddRange(newQuads);
    }
#endregion

}

