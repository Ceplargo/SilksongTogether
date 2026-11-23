using System.Collections.Generic;
using System.Linq;
using SSMP.Game.Client.Save.Complex;
using Xunit;

namespace SSMP.Tests;

/// <summary>
/// (coop, optimization) The item-container poll's no-change test: it may say "unchanged" only where Diff finds no change.
/// </summary>
public class ProjectionUnchangedTests {
    private static readonly ComplexFieldSpec Collectables = ComplexFieldSpecs.All["Collectables"];

    private static Projection P(params (string Entry, int Amount)[] values) {
        var projection = new Projection();
        foreach (var (entry, amount) in values) {
            projection[entry] = new Dictionary<string, int> { ["Amount"] = amount, ["AmountWhileHidden"] = 0 };
        }

        return projection;
    }

    private static List<(string, Dictionary<string, int?>)> Read(params (string Entry, int? Amount)[] values) {
        var entries = new List<(string, Dictionary<string, int?>)>();
        foreach (var (entry, amount) in values) {
            entries.Add((entry, new Dictionary<string, int?> { ["Amount"] = amount, ["AmountWhileHidden"] = 0 }));
        }

        return entries;
    }

    private static int? Value(Dictionary<string, int?> data, string member) =>
        data.TryGetValue(member, out var value) ? value : null;

    private static bool Unchanged(Projection last, List<(string, Dictionary<string, int?>)> now) =>
        ProjectionOps.Unchanged(last, Collectables, now, Value);

    private static bool DiffEmpty(Projection last, List<(string Entry, Dictionary<string, int?> Data)> now) {
        var read = new Projection();
        foreach (var (entry, data) in now) {
            var members = new Dictionary<string, int>();
            foreach (var (member, value) in data) {
                if (value is { } v) members[member] = v;
            }

            read[entry] = members;
        }

        return ProjectionOps.Diff(last, read, Collectables).Count == 0;
    }

    [Fact]
    public void The_same_values_are_unchanged() {
        var last = P(("Simple Key", 2), ("Pilgrim Rag", 12));
        var now = Read(("Simple Key", 2), ("Pilgrim Rag", 12));
        Assert.True(DiffEmpty(last, now));
        Assert.True(Unchanged(last, now));
    }

    [Fact]
    public void A_changed_amount_is_a_change() {
        var last = P(("Simple Key", 2), ("Pilgrim Rag", 12));
        var now = Read(("Simple Key", 2), ("Pilgrim Rag", 11));
        Assert.False(DiffEmpty(last, now));
        Assert.False(Unchanged(last, now));
    }

    [Fact]
    public void A_new_entry_with_an_amount_is_a_change() {
        var last = P(("Simple Key", 2));
        var now = Read(("Simple Key", 2), ("Flintgem", 1));
        Assert.False(DiffEmpty(last, now));
        Assert.False(Unchanged(last, now));
    }

    [Fact]
    public void A_new_entry_of_zeros_is_no_change_as_in_Diff() {
        var last = P(("Simple Key", 2));
        var now = Read(("Simple Key", 2), ("Flintgem", 0));
        Assert.True(DiffEmpty(last, now));
        Assert.True(Unchanged(last, now));
    }

    [Fact]
    public void An_entry_of_last_not_read_takes_the_full_read() {
        var last = P(("Simple Key", 2), ("Pilgrim Rag", 12));
        Assert.False(Unchanged(last, Read(("Simple Key", 2))));
    }

    [Fact]
    public void A_member_the_data_lacks_reads_as_zero() {
        Assert.True(Unchanged(P(("Simple Key", 0)), Read(("Simple Key", null))));
        Assert.False(Unchanged(P(("Simple Key", 3)), Read(("Simple Key", null))));
    }

    [Fact]
    public void The_test_allocates_nothing_per_entry() {
        // mini PC job 111: a foreach over SharedMembers.Keys boxed a new enumerator for every entry, 5 times a second
        static (Projection, List<(string, Dictionary<string, int?>)>) Full(int count) {
            var values = Enumerable.Range(0, count).Select(i => ($"Item {i}", i % 7)).ToArray();
            return (P(values), Read(values.Select(v => (v.Item1, (int?) v.Item2)).ToArray()));
        }

        long Bytes((Projection Last, List<(string, Dictionary<string, int?>)> Now) lists) {
            var before = System.GC.GetAllocatedBytesForCurrentThread();
            Assert.True(Unchanged(lists.Last, lists.Now));
            return System.GC.GetAllocatedBytesForCurrentThread() - before;
        }

        var (one, many) = (Full(1), Full(200));
        Bytes(many);  // the reused set grows once
        var perEntry = (Bytes(many) - Bytes(one)) / 199.0;
        Assert.True(perEntry < 4, $"{perEntry:F1} bytes an entry");
    }

    [Fact]
    public void An_entry_read_twice_does_not_stand_for_a_missing_one() {
        var last = P(("Simple Key", 2), ("Pilgrim Rag", 12));
        Assert.False(Unchanged(last, Read(("Simple Key", 2), ("Simple Key", 2))));
    }
}
