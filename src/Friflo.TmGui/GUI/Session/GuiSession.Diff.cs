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
        if (buffers.quadHashesBuffer.Length < quads.Length) {
            buffers.quadHashesBuffer = new int[quads.Length];
        }
        var newHashes = buffers.quadHashesBuffer.AsSpan(0, quads.Length);
        for (int n= 0; n < quads.Length; n++) {
            newHashes[n] = quads[n].GetHashCode();
        }
        var clientQuads = CollectionsMarshal.AsSpan(clientQuadList);
        SequenceDiff.TryComputeChanges(clientQuads, newHashes, 100000, buffers.changeList, out int diffValueCount);

        DebugVerifyHashDiff(clientQuads, newHashes, diffValueCount);
            
        clientQuadList.Clear();
        clientQuadList.AddRange(newHashes);
        
        if (buffers.changeList.Count == 0) {
            return vertices;
        }
        
        if (buffers.quadBuffer.Length < diffValueCount) {
            buffers.quadBuffer = new VertexQuad[diffValueCount];
        }
        var diffQuads = buffers.quadBuffer.AsSpan(0, diffValueCount);
        diffQuads = SequenceDiff.AppendDiffValues(buffers.changeList, quads, diffQuads);
        
        Debug.Assert(diffQuads.Length == diffValueCount);
        
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
        if (buffers.changeList.Count == 0) return;
        
        if (debugHashDiff.Length < diffValueCount) {
            debugHashDiff = new int[diffValueCount];
        }
        var diffQuads = debugHashDiff.AsSpan(0, diffValueCount);
        diffQuads = SequenceDiff.AppendDiffValues(buffers.changeList, quads, diffQuads);
        
        Debug.Assert(diffQuads.Length == diffValueCount);
        
        if (debugHashTarget.Length < quads.Length) {
            debugHashTarget = new int[quads.Length];
        }
        var hashTarget = debugHashTarget.AsSpan(0, quads.Length);
        
        SequenceDiff.ApplyChanges(clientQuads, buffers.changeList, diffQuads, hashTarget);

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
        if (buffers.changeList.Count == 0) return;
        
        ReadOnlySpan<VertexQuad> clientQuads = CollectionsMarshal.AsSpan(debugClientQuads);
        
        if (debugClientTarget.Length < quads.Length) {
            debugClientTarget = new VertexQuad[quads.Length];
        }
        var clientTarget = debugClientTarget.AsSpan(0, clientQuadList.Count);
        SequenceDiff.ApplyChanges(clientQuads, buffers.changeList, diffQuads, clientTarget);

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

