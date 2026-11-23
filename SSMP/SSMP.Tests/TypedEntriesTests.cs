using System.Collections.Generic;
using System.Linq;
using SSMP.Game.Client.Save.Complex;
using Xunit;

namespace SSMP.Tests;

/// <summary>
/// (coop, optimization) Item containers read through getters made once per type: the same projection the reflection read
/// gives, and no garbage per entry in the poll's no-change test (mini PC job 113: about 100 to 200 bytes an entry left).
/// </summary>
public class TypedEntriesTests {
    // shaped as the game's: a SerializableNamedList's Enumerate() of (name, struct data), and the journal's Dictionary
    public struct ItemData {
        public int Amount;
        public bool IsSeen;
        public int AmountWhileHidden;
        public float Weight;
    }

    public class ItemList {
        public readonly List<KeyValuePair<string, ItemData>> Items = new();

        public IEnumerable<KeyValuePair<string, ItemData>> Enumerate() => Items;
    }

    public class Journal {
        public Dictionary<string, ItemData> Dictionary = new();
    }

    public class ClassData {
        public int Amount;
    }

    public class ClassList {
        public IEnumerable<KeyValuePair<string, ClassData>> Enumerate() => [];
    }

    private static readonly ComplexFieldSpec Spec =
        new("Items", new Dictionary<string, MergeRule> { ["Amount"] = MergeRule.Add, ["AmountWhileHidden"] = MergeRule.Add });

    private static readonly ComplexFieldSpec WithFlags = new("Items", new Dictionary<string, MergeRule> {
        ["Amount"] = MergeRule.Add, ["IsSeen"] = MergeRule.Max, ["Weight"] = MergeRule.Last, ["Missing"] = MergeRule.Max
    });

    private static ItemList List(int count) {
        var list = new ItemList();
        for (var i = 0; i < count; i++) {
            list.Items.Add(new KeyValuePair<string, ItemData>($"Item {i}", new ItemData { Amount = i % 7, AmountWhileHidden = 1 }));
        }

        return list;
    }

    [Fact]
    public void Reads_as_the_reflection_read_does() {
        var list = new ItemList();
        list.Items.Add(new KeyValuePair<string, ItemData>("Key", new ItemData { Amount = 3, IsSeen = true, Weight = 2.5f }));
        list.Items.Add(new KeyValuePair<string, ItemData>("", new ItemData { Amount = 9 }));
        var typed = TypedEntries.For(typeof(ItemList), WithFlags);
        Assert.NotNull(typed);
        var read = typed!.Read(list, WithFlags);
        // bools as 0 or 1, other types 0, a member the data lacks left out, an entry with no name left out
        Assert.Equal(["Key"], read.Keys);
        Assert.Equal(new Dictionary<string, int> { ["Amount"] = 3, ["IsSeen"] = 1, ["Weight"] = 0 }, read["Key"]);
    }

    [Fact]
    public void The_journal_reads_through_its_dictionary() {
        var journal = new Journal { Dictionary = { ["Mossgrub"] = new ItemData { Amount = 4 } } };
        var typed = TypedEntries.For(typeof(Journal), Spec);
        Assert.NotNull(typed);
        Assert.Equal(4, typed!.Read(journal, Spec)["Mossgrub"]["Amount"]);
    }

    [Fact]
    public void Unchanged_only_where_the_read_is_the_same() {
        var list = List(5);
        var typed = TypedEntries.For(typeof(ItemList), Spec)!;
        var last = typed.Read(list, Spec);
        Assert.True(typed.Unchanged(list, last, Spec));
        list.Items[2] = new KeyValuePair<string, ItemData>("Item 2", new ItemData { Amount = 99 });
        Assert.False(typed.Unchanged(list, last, Spec));
    }

    [Fact]
    public void Data_that_is_a_class_keeps_the_reflection_read() {
        Assert.Null(TypedEntries.For(typeof(ClassList), Spec));
        Assert.Null(TypedEntries.For(typeof(string), Spec));
    }

    [Fact]
    public void The_no_change_test_allocates_nothing_per_entry() {
        var typed = TypedEntries.For(typeof(ItemList), Spec);
        Assert.NotNull(typed);

        long Bytes(ItemList list) {
            var last = typed!.Read(list, Spec);
            Assert.True(typed.Unchanged(list, last, Spec));
            var before = System.GC.GetAllocatedBytesForCurrentThread();
            Assert.True(typed.Unchanged(list, last, Spec));
            return System.GC.GetAllocatedBytesForCurrentThread() - before;
        }

        var (one, many) = (List(1), List(200));
        Bytes(many);
        var perEntry = (Bytes(many) - Bytes(one)) / 199.0;
        Assert.True(perEntry < 4, $"{perEntry:F1} bytes an entry");
    }
}
