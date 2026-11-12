using BossRule;
using Xunit;

namespace BossRule.Tests;

public class BenchBellRuleTests {
    // probe_bench_bell (0.3.21): a strike took the striker's bell from Idle to Hit Recovery in one frame
    [Fact]
    public void A_bell_leaving_idle_was_struck() => Assert.True(BenchBellRule.Struck("Idle", "Hit Recovery"));

    [Fact]
    public void A_bell_coming_to_idle_was_not() => Assert.False(BenchBellRule.Struck("Init", "Idle"));

    [Fact]
    public void A_bell_back_from_its_recovery_was_not() => Assert.False(BenchBellRule.Struck("Hit Recovery", "Idle"));

    // probe_bell_dir: Atk Dir 180 gave Hit Force -12.5, 0 gave 12.5
    [Fact]
    public void A_side_hit_keeps_its_direction() {
        Assert.Equal(180f, BenchBellRule.Side(180f, -12.5f));
        Assert.Equal(0f, BenchBellRule.Side(0f, 12.5f));
    }

    [Fact]
    public void An_up_hit_goes_the_way_its_force_took() {
        Assert.Equal(180f, BenchBellRule.Side(90f, -9f));
        Assert.Equal(0f, BenchBellRule.Side(270f, 9f));
    }

    [Fact]
    public void Only_a_waiting_bell_follows() {
        Assert.True(BenchBellRule.Echoes("Idle"));
        Assert.False(BenchBellRule.Echoes("Hit Recovery"));
        Assert.False(BenchBellRule.Echoes(null));
    }
}
