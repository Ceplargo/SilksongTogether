using SSMP.Game.Client.Entity.Action;
using Xunit;

namespace SSMP.Tests;

public class DelayedSwitchTests {
    // a stand-in for a game object: whether it still exists, its own active flag, and how often it was switched
    private sealed class Target {
        public bool Alive = true;
        public bool Active;
        public int Reads;
        public int Switches;
    }

    private static bool Run(Target target, bool activate, out bool initial) =>
        DelayedSwitch.Run(target, t => t.Alive, t => {
            t.Reads++;
            return t.Active;
        }, (t, on) => {
            t.Switches++;
            t.Active = on;
        }, activate, out initial);

    [Fact]
    public void A_delayed_switch_of_a_destroyed_object_does_nothing() {
        // the soak (2026-10-31): the friend's game read activeSelf of an object destroyed during the switch's delay
        var gone = new Target { Alive = false };
        Assert.False(Run(gone, true, out _));
        Assert.Equal(0, gone.Reads);
        Assert.Equal(0, gone.Switches);
    }

    [Fact]
    public void A_delayed_switch_of_a_live_object_switches_it_and_keeps_its_flag_before() {
        var live = new Target { Active = false };
        Assert.True(Run(live, true, out var initial));
        Assert.False(initial);
        Assert.True(live.Active);
        Assert.Equal(1, live.Switches);
    }
}
