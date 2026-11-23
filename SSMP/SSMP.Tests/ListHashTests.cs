using System;
using System.Collections.Generic;
using System.Linq;
using SSMP.Game.Client.Save;
using Xunit;

namespace SSMP.Tests;

public class ListHashTests {
    // SaveManager's hash before (its GetListHashCode; a HashSet went through ToList first)
    private static int Old<T>(List<T>? list) =>
        list == null || list.Count == 0 ? 0 : list.Select(item => item?.GetHashCode() ?? 0).Aggregate((a, b) => a ^ b);

    [Fact]
    public void It_gives_the_same_hash_as_before() {
        var strings = new List<string> { "Bone_01", "Tut_01", "Coral_02" };
        var withNull = new List<string?> { "a", null, "b" };
        var ints = new List<int> { 3, 7, 7, 12 };
        var set = new HashSet<string> { "Bonetown", "Mosstown", "Bellhart" };
        Assert.Equal(Old(strings), ListHash.Of(strings));
        Assert.Equal(Old(withNull), ListHash.Of(withNull));
        Assert.Equal(Old(ints), ListHash.Of(ints));
        Assert.Equal(Old(set.ToList()), ListHash.Of(set));
        Assert.Equal(0, ListHash.Of(new List<int>()));
        Assert.Equal(0, ListHash.Of((List<string>?) null));
        Assert.Equal(0, ListHash.Of((HashSet<string>?) null));
        Assert.NotEqual(0, ListHash.Of(set));
    }

    [Fact]
    public void Reading_each_frame_allocates_nothing() {
        var set = new HashSet<string>(Enumerable.Range(0, 200).Select(i => $"item {i}"));
        var list = Enumerable.Range(0, 200).ToList();
        ListHash.Of(set);
        ListHash.Of(list);
        // the least of three passes: now and then, under load, the runtime's own compiles (tier-up, a loop's
        // recompile) allocated 5 to 7 KB on this thread in one pass (4 of about 190 stress runs, 2026-11-21); an
        // allocation of ListHash's own shows in every pass
        var least = long.MaxValue;
        for (var pass = 0; pass < 3; pass++) {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var frame = 0; frame < 100; frame++) {
                ListHash.Of(set);
                ListHash.Of(list);
            }

            least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        Assert.Equal(0, least);
    }
}
