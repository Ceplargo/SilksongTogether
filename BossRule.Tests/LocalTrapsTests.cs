using BossRule;
using Xunit;

namespace BossRule.Tests;

// 0.3.20 Part C cause no. 2: The Marrow's crumbling platforms, chain-drop, Collapser Small, explode walls,
// tool_metal_deposit and falling boulders broke, rumbled or fell only in the game of the player who set them off
// (probe_traps, the object sweeps, the room walk): on the other screen a player fell through or walked past what was
// still whole, and a falling boulder hurt in one game only.
public class LocalTrapsTests {
    [Fact]
    public void A_crumbling_platform_stood_on_tells_the_room_its_crumble() {
        Assert.Equal("CRUMBLE", LocalTraps.Sets("bone_crumble_plat", "bone_plat_01_crumble (3)", "Idle", "Crumble Antic"));
        Assert.Equal("CRUMBLE", LocalTraps.Sets("bone_crumble_plat", "bone_plat_01_crumble_small", "Check Collision", "Crumble Antic"));
        Assert.Null(LocalTraps.Sets("bone_crumble_plat", "bone_plat_01_crumble (3)", "Crumble Antic", "Break"));
    }

    [Fact]
    public void Each_kind_is_set_off_by_its_own_event() {
        Assert.Equal("LAND", LocalTraps.Sets("Control", "Chain Drop Platform", "Idle", "Break Antic"));
        Assert.Equal("BREAK", LocalTraps.Sets("Control", "Chain Drop Platform", "Idle", "Slash Break"));
        Assert.Equal("BREAK", LocalTraps.Sets("Control", "Collapser Small (1)", "Idle", "Antic Type"));
        Assert.Equal("DROP", LocalTraps.Sets("Control", "Bone_Boulder (2)", "Shake", "Drop Antic"));
        Assert.Equal("STRIKE", LocalTraps.Sets("Control", "explode_wall (2)", "Idle", "Strike"));
        Assert.Equal("HIT", LocalTraps.Sets("Control", "tool_metal_deposit", "Idle", "Hit Register Event"));
    }

    [Fact]
    public void Other_objects_and_moves_tell_nobody() {
        Assert.Null(LocalTraps.Sets("Control", "Bone Beast", "Idle", "Strike"));
        // the Skull King's boulders drop on his own attack, which his fight's follow already sends (StageFollowed)
        Assert.Null(LocalTraps.Sets("Control", "Bone_Boulder SK (19)", "Shake", "Drop Antic"));
        Assert.False(LocalTraps.Echoes("Control", "Bone_Boulder SK (19)", "Idle"));
        Assert.Null(LocalTraps.Sets("Control", "Bone_Boulder", "Drop Antic", "Drop"));
        Assert.Null(LocalTraps.Sets("Control", "Chain Drop Platform", "Init", "Idle"));
    }

    [Fact]
    public void Another_games_word_sets_off_only_one_still_waiting() {
        Assert.True(LocalTraps.Echoes("bone_crumble_plat", "bone_plat_01_crumble", "Idle"));
        Assert.True(LocalTraps.Echoes("Control", "Bone_Boulder", "Shake"));
        Assert.False(LocalTraps.Echoes("bone_crumble_plat", "bone_plat_01_crumble", "Break"));
        Assert.False(LocalTraps.Echoes("Control", "Bone Beast", "Idle"));
    }
}
