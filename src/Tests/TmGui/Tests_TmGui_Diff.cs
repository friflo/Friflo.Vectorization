using System;
using System.Collections.Generic;
using Friflo.TmGui;
using Friflo.TmGui.Session;
using NUnit.Framework;


// ReSharper disable SuggestVarOrType_Elsewhere
// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable UnusedMember.Local
// ReSharper disable once InconsistentNaming
namespace Tests.TmGui;

public static class Tests_TmGui_Diff
{
    private const int Lookahead = 64;
            
    [Test]
    public static void Tests_TmGui_Diff_Changes()
    {
        var change = new SeqChange(SeqChangeType.Insert, 10, 20);
        Assert.That(change.Type,    Is.EqualTo(SeqChangeType.Insert));
        Assert.That(change.Start,   Is.EqualTo(10));
        Assert.That(change.Length,  Is.EqualTo(20));
        
        Assert.That(change.ToString(),  Is.EqualTo("Insert [10..20]"));
        
        
        int[] start     = [1, 2, 3,    4, 5, 6];
        int[] target1   = [1, 2, 3, 9, 4, 5, 6];
        int[] target2   = [1,    3,    4, 5, 6];
        int[] target3   = [1, 2, 3,    4, 8, 6];
        
        int[] diffBuffer    = [];
        int[] targetBuffer  = [];
        
        var changes = new List<SeqChange>();

        
        // --- target1 / Insert
        SequenceDiff.TryComputeChanges(start, target1, 10, Lookahead, changes, out int diffValueCount); 
        Assert.That(changes.Count,      Is.EqualTo(1));
        Assert.That(changes[0].Label,   Is.EqualTo("Insert [3..1]"));
        Assert.That(diffValueCount,     Is.EqualTo(1));
        
        var diff = SequenceDiff.FillDiffValues(changes, target1, ref diffBuffer, diffValueCount);
        Assert.That(diff[0],       Is.EqualTo(9));
        var newTarget1 = SequenceDiff.ApplyChanges(start, changes, diffBuffer, ref targetBuffer, 7);
        Assert.That(newTarget1.ToArray(),  Is.EqualTo(target1));
        
        // --- target2 / Remove
        SequenceDiff.TryComputeChanges(start, target2, 10, Lookahead, changes, out diffValueCount); 
        Assert.That(changes.Count,      Is.EqualTo(1));
        Assert.That(changes[0].Label,   Is.EqualTo("Remove [1..1]"));
        Assert.That(diffValueCount,     Is.EqualTo(0));
        
        SequenceDiff.FillDiffValues(changes, target2, ref diffBuffer, diffValueCount);
        var newTarget2 = SequenceDiff.ApplyChanges(start, changes, diffBuffer, ref targetBuffer, 5);
        Assert.That(newTarget2.ToArray(), Is.EqualTo(target2));

        
        // --- target3 / Modify
        SequenceDiff.TryComputeChanges(start, target3, 10, Lookahead, changes, out diffValueCount); 
        Assert.That(changes.Count,      Is.EqualTo(1));
        Assert.That(changes[0].Label,   Is.EqualTo("Modify [4..1]"));
        Assert.That(diffValueCount,     Is.EqualTo(1));
        
        SequenceDiff.FillDiffValues(changes, target3, ref diffBuffer, diffValueCount);
        Assert.That(diffBuffer[0],      Is.EqualTo(8));
        var newTarget3 = SequenceDiff.ApplyChanges(start, changes, diffBuffer, ref targetBuffer, 6);
        Assert.That(newTarget3.ToArray(), Is.EqualTo(target3));
    }
    
    [Test]
    public static void Tests_TmGui_Diff_Changes_perf()
    {
        var start   = new int[2000];
        var target1 = new int[2000];
        for (int n = 0; n < 2000; n++)
        {
            start[n]    = n;
            target1[n]  = n;
        }
        target1[1000] = 99;
        
        var changes = new List<SeqChange>();
        SequenceDiff.TryComputeChanges(start, target1, int.MaxValue, Lookahead, changes, out _);
        Assert.That(changes.Count,      Is.EqualTo(1));
        Assert.That(changes[0].Label,   Is.EqualTo("Modify [1000..1]"));
        
        const int repeat  = 10_000_000; // 10_000_000, Lookahead = 64 - 10.5 sec
        for (int n = 0; n < repeat; n++)
        {
            SequenceDiff.TryComputeChanges(start, target1, int.MaxValue, Lookahead, changes, out _);
        }
    }
    
    [Test]
    public static void Tests_TmGui_Diff_Quad_GetHashCode()
    {
        var quads = new VertexQuad[2000];
        const int repeat  = 10; // 1_000_000 - 5.1 sec
        
        for (int n = 0; n < repeat; n++) {
            foreach (ref var quad in quads.AsSpan())
            {
                _ = quad.GetQuadHash();
            }
        }
    }
}