using SSMP.Game.Client.Entity.Component;
using Xunit;

namespace SSMP.Tests;

public class FlungThrowsTests {
    [Fact]
    public void The_Widows_slashes_fling_her_bells() {
        Assert.True(FlungThrows.Has("Spinner Boss"));
        Assert.Equal("Spinner AtkBell", FlungThrows.Flung("Spinner Boss"));
    }

    [Fact]
    public void Both_her_vertical_slashes_are_her_throws() {
        Assert.True(FlungThrows.ThrownBy("Spinner Boss", "Spinner_vertical_slash_long_new(Clone)"));
        Assert.True(FlungThrows.ThrownBy("Spinner Boss", "Spinner_vertical_slash(Clone)"));
    }

    [Fact]
    public void Her_harp_shots_are_her_throws_too() {
        // each shot moves to a random place before it flings its bell (Pos L/R: RandomFloat, SetPosition)
        Assert.True(FlungThrows.ThrownBy("Spinner Boss", "Spinner BellShot 1"));
        Assert.True(FlungThrows.ThrownBy("Spinner Boss", "Spinner BellShot 2"));
    }

    [Fact]
    public void Other_objects_and_enemies_are_not() {
        Assert.False(FlungThrows.ThrownBy("Spinner Boss", "Spinner Boss"));
        Assert.False(FlungThrows.ThrownBy("Bone Beast", "Spinner_vertical_slash(Clone)"));
        Assert.False(FlungThrows.Has("Bone Beast"));
        Assert.Null(FlungThrows.Flung("Bone Beast"));
    }

    [Fact]
    public void The_Last_Judges_charge_drops_her_bombs() {
        // her Charge spawns one at her Bomb Point, then one every 0.2 s (SpawnObjectFromGlobalPoolOverTime)
        Assert.True(FlungThrows.Has("Last Judge"));
        Assert.Equal("Last Judge Charge Bomb", FlungThrows.Flung("Last Judge"));
        Assert.True(FlungThrows.ThrownBy("Last Judge", "Last Judge"));
    }

    [Fact]
    public void Only_what_is_flung_is_followed() {
        Assert.True(FlungThrows.IsFlungObject("Last Judge", "Last Judge Charge Bomb(Clone)"));
        Assert.True(FlungThrows.IsFlungObject("Spinner Boss", "Spinner AtkBell(Clone)"));
        Assert.False(FlungThrows.IsFlungObject("Last Judge", "Last Judge Stomp Wave(Clone)"));
        Assert.False(FlungThrows.IsFlungObject("Bone Beast", "Last Judge Charge Bomb(Clone)"));
    }

    [Fact]
    public void A_throw_gets_one_copy_until_the_host_ends_it() {
        // the Last Judge's bomb copy recycled itself before the host's did; the next update still listed the host's,
        // and a second copy put straight into its flare hurt for 0.5 s where the host's game had none (NOTES 339)
        Assert.True(FlungThrows.NeedsCopy(hasCopy: false, copiedBefore: false));
        Assert.False(FlungThrows.NeedsCopy(hasCopy: false, copiedBefore: true));
        Assert.False(FlungThrows.NeedsCopy(hasCopy: true, copiedBefore: true));
    }

    [Fact]
    public void Only_the_marker_says_flung() {
        Assert.True(FlungThrows.IsFlung(FlungThrows.Marker));
        Assert.False(FlungThrows.IsFlung(255));
        Assert.False(FlungThrows.IsFlung(0));
    }
}
