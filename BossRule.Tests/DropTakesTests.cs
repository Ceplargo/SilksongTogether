using BossRule;
using Xunit;

namespace BossRule.Tests;

public class DropTakesTests {
    [Fact]
    public void The_first_take_of_a_drop_counts_and_a_second_does_not() {
        var takes = new DropTakes();
        Assert.True(takes.Take("Bone_04", "Rock Roller_item_Rock Rollers"));
        Assert.False(takes.Take("Bone_04", "Rock Roller_item_Rock Rollers"));
    }

    [Fact]
    public void Drops_of_other_enemies_or_rooms_count_apart() {
        var takes = new DropTakes();
        Assert.True(takes.Take("Bone_04", "Rock Roller_item_Rock Rollers"));
        Assert.True(takes.Take("Bone_04", "Rock Roller (1)_item_Rock Rollers"));
        Assert.True(takes.Take("Bone_06", "Rock Roller_item_Rock Rollers"));
    }
}
