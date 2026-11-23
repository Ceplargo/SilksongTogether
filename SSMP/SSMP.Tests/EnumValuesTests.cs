using System;
using SSMP.Util;
using Xunit;

namespace SSMP.Tests;

public class EnumValuesTests {
    [Fact]
    public void They_are_the_enums_values_in_GetValues_order() {
        Assert.Equal((DayOfWeek[]) Enum.GetValues(typeof(DayOfWeek)), EnumValues<DayOfWeek>.All);
    }

    [Fact]
    public void Reading_them_allocates_nothing() {
        var length = EnumValues<DayOfWeek>.All.Length;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++) {
            length += EnumValues<DayOfWeek>.All.Length;
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(707, length);
    }
}
