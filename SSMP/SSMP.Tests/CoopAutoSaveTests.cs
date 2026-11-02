using SSMP.Game.Server;
using Xunit;

namespace SSMP.Tests;

public class CoopAutoSaveTests {
    private static double After(double seconds) => 1000 + seconds;

    [Fact]
    public void Saves_after_the_interval_with_a_friend_in() {
        Assert.False(CoopAutoSave.Due(After(CoopAutoSave.IntervalSeconds - 1), 1000, 1, false, true));
        Assert.True(CoopAutoSave.Due(After(CoopAutoSave.IntervalSeconds), 1000, 1, false, true));
    }

    [Fact]
    public void Not_alone() => Assert.False(CoopAutoSave.Due(After(10_000), 1000, 0, false, true));

    [Fact]
    public void At_once_when_a_friend_leaves() => Assert.True(CoopAutoSave.Due(After(1), 1000, 0, true, true));

    [Fact]
    public void Not_while_the_hero_is_not_in_control() {
        Assert.False(CoopAutoSave.Due(After(10_000), 1000, 1, false, false));
        Assert.False(CoopAutoSave.Due(After(1), 1000, 0, true, false));
    }
}
