using SSMP.Game.Client.Entity.Component;
using Xunit;

namespace SSMP.Tests;

public class BlockedHitsTests {
    // Bone_09's Dock Flyer blocking a DF Lava Rock in the scene host's game: its block showed (its tinkTimer had run out
    // and the block set it), so its copies show it too
    [Fact]
    public void The_scene_hosts_shown_block_of_a_world_hit_is_sent() =>
        Assert.True(BlockedHits.Sends(true, BlockedHits.World(false, false, false), true, 0f, 0.1f));

    // a copy's game sends none: the scene host's game runs the enemy's hits
    [Fact]
    public void A_copys_block_is_not_sent() => Assert.False(BlockedHits.Sends(false, true, true, 0f, 0.1f));

    // a player's hit is no world hit (its block is the hitter's game's: SendsPlayer)
    [Fact]
    public void A_players_hit_is_no_world_hit() {
        Assert.False(BlockedHits.World(true, false, false));
        Assert.False(BlockedHits.World(false, true, false));
        // a hero's thrown tool: its root is no player, but its damage is a hero's
        Assert.False(BlockedHits.World(false, false, true));
    }

    [Fact]
    public void A_world_hit_is_not_sent_unless_blocked() => Assert.False(BlockedHits.Sends(true, true, false, 0f, 0f));

    // the game shows at most one block each 0.1 s (its tinkTimer): one it did not show is not sent
    [Fact]
    public void A_block_in_the_last_ones_tink_time_is_not_sent() =>
        Assert.False(BlockedHits.Sends(true, true, true, 0.05f, 0.03f));

    // a copy leaves out its own block of a world object's hit (the scene host's entry shows it), not of a player's
    [Fact]
    public void A_copy_leaves_out_its_own_block_of_a_world_hit_only() {
        Assert.True(BlockedHits.LeavesOut(true, true));
        Assert.False(BlockedHits.LeavesOut(true, false));
        Assert.False(BlockedHits.LeavesOut(false, true));
    }

    // Bone_06's Rock Roller starting to roll: the hitter's copy blocked its swing and the scene host's roller did not, so
    // it clinked on the hitter's screen only. The hitter's game sends the block of its own hit it shows, whichever game
    // runs the enemy
    [Fact]
    public void A_players_shown_block_of_their_own_hit_is_sent() =>
        Assert.True(BlockedHits.SendsPlayer(true, true, 0f, 0.1f));

    [Fact]
    public void Another_players_hit_is_not_sent_by_this_game() =>
        Assert.False(BlockedHits.SendsPlayer(false, true, 0f, 0.1f));

    [Fact]
    public void A_players_hit_not_blocked_or_not_shown_is_not_sent() {
        Assert.False(BlockedHits.SendsPlayer(true, false, 0f, 0f));
        Assert.False(BlockedHits.SendsPlayer(true, true, 0.05f, 0.03f));
    }

    // a game shows no block of its own for another player's hit (the hitter's entry shows it), so the two don't double
    [Fact]
    public void A_game_leaves_out_its_own_block_of_another_players_hit_only() {
        Assert.True(BlockedHits.LeavesOutPlayer(true));
        Assert.False(BlockedHits.LeavesOutPlayer(false));
    }

    // HealthManager.Invincible's flash: on the side of its box the hit came from, at the source's height (from below or
    // above: at the source's x, within the box), turned by the hit's way; with no box, at the enemy
    [Fact]
    public void The_flash_goes_where_the_game_puts_it() {
        Assert.Equal((9f, 3f, 0f), BlockedHits.Flash(0, 10f, 2f, true, 0f, 0f, 2f, 4f, 5f, 3f));
        Assert.Equal((11f, 3f, 180f), BlockedHits.Flash(2, 10f, 2f, true, 0f, 0f, 2f, 4f, 15f, 3f));
        Assert.Equal((10.5f, 0f, 90f), BlockedHits.Flash(1, 10f, 2f, true, 0f, 0f, 2f, 4f, 10.5f, -3f));
        Assert.Equal((11f, 4f, 270f), BlockedHits.Flash(3, 10f, 2f, true, 0f, 0f, 2f, 4f, 14f, 9f));
        Assert.Equal((10.5f, 2.5f, 0f), BlockedHits.Flash(0, 10f, 2f, true, 1.5f, 0f, 2f, 4f, 5f, 2.5f));
        Assert.Equal((10f, 2f, 0f), BlockedHits.Flash(1, 10f, 2f, false, 0f, 0f, 0f, 0f, 3f, 3f));
    }
}
