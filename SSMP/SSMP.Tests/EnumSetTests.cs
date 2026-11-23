using System;
using System.Collections.Generic;
using SSMP.Util;
using Xunit;

namespace SSMP.Tests;

public class EnumSetTests {
    private enum Small : byte { A, B, C }

    [Fact]
    public void It_answers_as_a_HashSet_does() {
        var set = new EnumSet<DayOfWeek>();
        var hash = new HashSet<DayOfWeek>();
        foreach (var (add, day) in new[] {
                     (true, DayOfWeek.Monday), (true, DayOfWeek.Monday), (true, DayOfWeek.Sunday),
                     (false, DayOfWeek.Monday), (false, DayOfWeek.Friday), (true, DayOfWeek.Saturday)
                 }) {
            Assert.Equal(add ? hash.Add(day) : hash.Remove(day), add ? set.Add(day) : set.Remove(day));
            Assert.Equal(hash.Count, set.Count);
            foreach (DayOfWeek each in Enum.GetValues(typeof(DayOfWeek))) {
                Assert.Equal(hash.Contains(each), set.Contains(each));
            }
        }

        set.Clear();
        Assert.Equal(0, set.Count);
        Assert.False(set.Contains(DayOfWeek.Sunday));
    }

    [Fact]
    public void A_byte_enum_works_too() {
        var set = new EnumSet<Small>();
        set.Add(Small.C);
        Assert.True(set.Contains(Small.C));
        Assert.False(set.Contains(Small.A));
    }

    [Fact]
    public void A_set_with_two_values_allocates_only_itself() {
        new EnumSet<DayOfWeek>().Add(DayOfWeek.Monday);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var set = new EnumSet<DayOfWeek>();
        set.Add(DayOfWeek.Monday);
        set.Add(DayOfWeek.Tuesday);
        Assert.True(GC.GetAllocatedBytesForCurrentThread() - before <= 32);
        Assert.Equal(2, set.Count);
    }
}
