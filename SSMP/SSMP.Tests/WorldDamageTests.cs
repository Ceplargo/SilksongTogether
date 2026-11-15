using SSMP.Game.Client.Entity.Component;
using Xunit;

namespace SSMP.Tests;

public class WorldDamageTests {
    // Coral_02's Sand Centipede on the friend's copy of the Coral Judge: the scene host's game applies it to the original
    [Fact]
    public void A_world_hit_on_a_copy_is_left_out() =>
        Assert.False(WorldDamage.Applies(true, BlockedHits.World(false, false, false)));

    // the scene host's game, or a game alone, applies the hazard to its own enemy as the game does
    [Fact]
    public void A_world_hit_on_the_original_is_applied() =>
        Assert.True(WorldDamage.Applies(false, BlockedHits.World(false, false, false)));

    // two players' hits in the same frame each count: each takes health in its own game, a copy's included
    [Fact]
    public void Each_players_hit_on_a_copy_is_applied() {
        Assert.True(WorldDamage.Applies(true, BlockedHits.World(true, false, false)));
        Assert.True(WorldDamage.Applies(true, BlockedHits.World(false, true, false)));
        // a hero's thrown tool: its root is no player, but its damage is a hero's
        Assert.True(WorldDamage.Applies(true, BlockedHits.World(false, false, true)));
    }
}
