using System.Collections.Generic;
using Friflo.TmGui.Http;
using NUnit.Framework;


// ReSharper disable SuggestVarOrType_Elsewhere
// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable UnusedMember.Local
// ReSharper disable once InconsistentNaming
namespace Tests.TmGui;

public static class Tests_TmGui_Diff
{
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
        int[] target3   = [1, 2, 3,    4, 9, 6];
        
        var changes = new List<SeqChange>();
        
        SequenceDiff.TryComputeChanges(start, target1, 10, changes); 
        Assert.That(changes.Count,      Is.EqualTo(1));
        Assert.That(changes[0].Label,   Is.EqualTo("Insert [3..1]"));
        
        SequenceDiff.TryComputeChanges(start, target2, 10, changes); 
        Assert.That(changes.Count,      Is.EqualTo(1));
        Assert.That(changes[0].Label,   Is.EqualTo("Remove [1..1]"));
        
        SequenceDiff.TryComputeChanges(start, target3, 10, changes); 
        Assert.That(changes.Count,      Is.EqualTo(1));
        Assert.That(changes[0].Label,   Is.EqualTo("Modify [4..1]"));
        
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
        SequenceDiff.TryComputeChanges(start, target1, 10, changes);
        Assert.That(changes.Count,      Is.EqualTo(1));
        Assert.That(changes[0].Label,   Is.EqualTo("Modify [1000..1]"));
        
        const int repeat  = 10; // 10_000_000 - 1.9 sec
        for (int n = 0; n < repeat; n++)
        {
            SequenceDiff.TryComputeChanges(start, target1, 10, changes);
        }
    }
}