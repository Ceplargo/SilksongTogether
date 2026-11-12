using SSMP.Game.Client;
using Xunit;

namespace SSMP.Tests;

public class HitNotesTests {
    [Fact]
    public void This_games_heros_hit_is_noted() => Assert.True(HitNotes.Notes(true, false, true));

    // the lava's hit on Bone_16's Bone Circler in the scene host's game
    [Fact]
    public void A_world_hit_is_noted() => Assert.True(HitNotes.Notes(false, false, true));

    // another player's copied swing: that player's game sends its own drop and kind
    [Fact]
    public void Another_players_hit_is_not_noted() => Assert.False(HitNotes.Notes(false, true, true));

    // Bone_07's Rock Roller: a trail pop's blast it is immune to, in the frame of the slash that took 5 of its health,
    // overwrote the slash's kind (the drop went out as an explosion, which the other game's roller ignores)
    [Fact]
    public void A_world_hit_that_took_no_health_is_not_noted() => Assert.False(HitNotes.Notes(false, false, false));

    // a hit it blocked, in the frame of another that took health
    [Fact]
    public void A_heros_hit_that_took_no_health_is_not_noted() => Assert.False(HitNotes.Notes(true, false, false));
}
