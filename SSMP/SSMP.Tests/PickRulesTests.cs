using SSMP.Game.Client.Entity.Action;
using Xunit;

namespace SSMP.Tests;

public class PickRulesTests {
    [Theory]
    [InlineData(-1, 3)]
    [InlineData(3, 3)]
    [InlineData(0, 0)]
    public void PickIn_OutsideTheList_GivesNone(int index, int count) => Assert.Null(PickRules.PickIn(index, count));

    [Fact]
    public void PickIn_UnknownPick_GivesNone() => Assert.Null(PickRules.PickIn(null, 3));

    [Fact]
    public void PickIn_InsideTheList_GivesThePick() => Assert.Equal(2, PickRules.PickIn(2, 3));
}
