using SSMP.Game.Client.Entity;
using Xunit;

namespace SSMP.Tests;

public class PoolLeftoversTests {
    // the Widow's slash spawned as her room unloaded: alive, its pool's prefab destroyed with the room
    [Fact]
    public void An_object_whose_prefab_is_gone_ends() => Assert.True(PoolLeftovers.Ends(true, false));

    // every other recycle is the game's own
    [Fact]
    public void An_object_whose_prefab_is_alive_is_the_games() => Assert.False(PoolLeftovers.Ends(true, true));

    [Fact]
    public void A_destroyed_object_is_the_games() {
        Assert.False(PoolLeftovers.Ends(false, false));
        Assert.False(PoolLeftovers.Ends(false, true));
    }
}
