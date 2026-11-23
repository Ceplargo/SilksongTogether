using SSMP.Game.Client.Entity.Action;
using Xunit;

namespace SSMP.Tests;

public class DelaySpawnsTests {
    // the Widow's Slams Outer: her last slash 0.161 s in, the state left in that frame; the copy's exit comes as its wait
    // is about to end
    [Fact]
    public void A_spawn_due_as_the_state_is_left_is_made() => Assert.True(DelaySpawns.AtExit(false, 0.155f, 0.161f));

    [Fact]
    public void A_spawn_the_host_left_before_is_not() => Assert.False(DelaySpawns.AtExit(false, 0.05f, 0.24f));

    [Fact]
    public void A_spawn_made_already_is_not_made_again() => Assert.False(DelaySpawns.AtExit(true, 0.3f, 0.1f));
}
