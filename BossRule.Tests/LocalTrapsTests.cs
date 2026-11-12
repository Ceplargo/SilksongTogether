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

    // 0.3.21 Part C cause 3: Bone_03's bone_plat_02_crumble platforms (the magnet_platform_v2 sounds) run the crumbling
    // platform's FSM under the name Control, which no kind listed: one broke in the walker's game only (the Marrow walk,
    // Bone_03 left2: probe_touch_sources)
    [Fact]
    public void A_second_kind_of_crumbling_platform_tells_the_room_its_crumble() {
        Assert.Equal("CRUMBLE", LocalTraps.Sets("Control", "bone_plat_02_crumble (6)", "Check Collision", "Crumble Antic"));
        Assert.True(LocalTraps.Echoes("Control", "bone_plat_02_crumble (6)", "Idle", "CRUMBLE"));
        Assert.False(LocalTraps.Echoes("Control", "bone_plat_02_crumble (6)", "Break", "CRUMBLE"));
    }

    [Fact]
    public void Other_objects_and_moves_tell_nobody() {
        Assert.Null(LocalTraps.Sets("Control", "Bone Beast", "Idle", "Strike"));
        // the Skull King's boulders drop on his own attack, which his fight's follow already sends (StageFollowed)
        Assert.Null(LocalTraps.Sets("Control", "Bone_Boulder SK (19)", "Shake", "Drop Antic"));
        Assert.False(LocalTraps.Echoes("Control", "Bone_Boulder SK (19)", "Idle", "DROP"));
        Assert.Null(LocalTraps.Sets("Control", "Bone_Boulder", "Drop Antic", "Drop"));
        Assert.Null(LocalTraps.Sets("Control", "Chain Drop Platform", "Init", "Idle"));
    }

    // 0.3.21: a player's hazard respawn sends the room's broken crumbling platforms back whole at once (HAZARD
    // RESPAWNED) in that player's game only: on the other screen they stayed broken about 1.7 s more (probe_traps,
    // probe_crumble_reset); and Bone_01's Collapser Small (1) goes Idle, Antic, Break, never the Antic Type listed
    [Fact]
    public void A_respawns_platform_reset_tells_the_room() {
        // its reset state passes inside one frame: a broken one seen going straight to Idle was reset
        Assert.Equal("HAZARD RESPAWNED", LocalTraps.Sets("bone_crumble_plat", "bone_plat_01_crumble (2)", "Break", "Idle"));
        Assert.Equal("HAZARD RESPAWNED",
            LocalTraps.Sets("bone_crumble_plat", "bone_plat_01_crumble (2)", "Core Antic", "Hazard Respawn Reset"));
        Assert.Null(LocalTraps.Sets("bone_crumble_plat", "bone_plat_01_crumble (2)", "Hazard Respawn Reset", "Idle"));
        // a rebuild's own end (Rebuild, then Idle) is no reset
        Assert.Null(LocalTraps.Sets("bone_crumble_plat", "bone_plat_01_crumble (2)", "Rebuild", "Idle"));
        Assert.Equal("BREAK", LocalTraps.Sets("Control", "Collapser Small (1)", "Idle", "Antic"));
    }

    // 0.3.21: Bone_03's bone_plat_02_crumble runs the same FSM under the name Control; a player's respawn reset it
    // in that game only, and the other game played its Rebuild (probe_traps falling, 2026-11-10_174448)
    [Fact]
    public void The_control_kinds_respawn_reset_tells_the_room_too() {
        Assert.Equal("HAZARD RESPAWNED", LocalTraps.Sets("Control", "bone_plat_02_crumble (4)", "Core Antic", "Idle"));
        Assert.Null(LocalTraps.Sets("Control", "bone_plat_02_crumble (4)", "Rebuild", "Idle"));
        Assert.True(LocalTraps.Echoes("Control", "bone_plat_02_crumble (4)", "Rebuild", "HAZARD RESPAWNED"));
        Assert.False(LocalTraps.Echoes("Control", "bone_plat_02_crumble (4)", "Idle", "HAZARD RESPAWNED"));
    }

    // 0.3.21: a hit's or respawn's pause stops the faller's clock, so its platform ran behind the other game's (the
    // project's 18:15 ruling 2): the setting-off game tells each step and the other game's platform waits for it
    [Fact]
    public void The_setting_off_game_tells_each_timed_step() {
        Assert.Equal("@Core Antic", LocalTraps.StepOf("bone_crumble_plat", "bone_plat_01_crumble_small", "Break", "Core Antic"));
        Assert.Equal("@Rebuild", LocalTraps.StepOf("Control", "bone_plat_02_crumble (4)", "Core Antic", "Rebuild"));
        Assert.Null(LocalTraps.StepOf("bone_crumble_plat", "bone_plat_01_crumble", "Crumble Antic", "Break"));
        Assert.Null(LocalTraps.StepOf("Control", "Bone_Boulder", "Break", "Core Antic"));
    }

    [Fact]
    public void Another_games_platform_waits_for_the_step_only_in_a_timed_state() {
        Assert.Equal("Break", LocalTraps.StepFrom("bone_crumble_plat", "bone_plat_01_crumble (2)", "@Core Antic"));
        Assert.Equal("Core Antic", LocalTraps.StepFrom("Control", "bone_plat_02_crumble (4)", "@Rebuild"));
        Assert.Null(LocalTraps.StepFrom("bone_crumble_plat", "bone_plat_01_crumble", "CRUMBLE"));
        Assert.Null(LocalTraps.StepFrom("Dialogue", "Caravan Lech", "@Wounded State"));
        Assert.Equal("Rebuild", LocalTraps.NextOf("bone_crumble_plat", "bone_plat_01_crumble", "Core Antic"));
        Assert.Null(LocalTraps.NextOf("bone_crumble_plat", "bone_plat_01_crumble", "Rebuild"));
        Assert.Null(LocalTraps.NextOf("bone_crumble_plat", "bone_plat_01_crumble", "Idle"));
    }

    [Fact]
    public void A_platform_both_heroes_set_off_has_one_step_teller() {
        Assert.True(LocalTraps.TellsSteps(true, true, true));   // the scene host's game
        Assert.False(LocalTraps.TellsSteps(true, true, false)); // the other game follows it
        Assert.True(LocalTraps.TellsSteps(true, false, false)); // one hero: that hero's game, host or not
        Assert.False(LocalTraps.TellsSteps(false, true, true)); // another game's set-off: follows
    }

    [Fact]
    public void The_wait_for_a_step_ends_a_second_past_its_own() {
        Assert.True(LocalTraps.HoldsStep(2.5f, 2f));
        Assert.True(LocalTraps.HoldsStep(2.99f, 2f));
        Assert.False(LocalTraps.HoldsStep(3.01f, 2f)); // the setting-off game left or its word was lost: it goes on
    }

    [Fact]
    public void Another_games_reset_resets_only_a_broken_platform() {
        Assert.True(LocalTraps.Echoes("bone_crumble_plat", "bone_plat_01_crumble", "Break", "HAZARD RESPAWNED"));
        Assert.True(LocalTraps.Echoes("bone_crumble_plat", "bone_plat_01_crumble", "Rebuild", "HAZARD RESPAWNED"));
        Assert.False(LocalTraps.Echoes("bone_crumble_plat", "bone_plat_01_crumble", "Idle", "HAZARD RESPAWNED"));
        Assert.False(LocalTraps.Echoes("Control", "Bone_Boulder", "Shake", "HAZARD RESPAWNED"));
    }

    [Fact]
    public void Another_games_word_sets_off_only_one_still_waiting() {
        Assert.True(LocalTraps.Echoes("bone_crumble_plat", "bone_plat_01_crumble", "Idle", "CRUMBLE"));
        Assert.True(LocalTraps.Echoes("Control", "Bone_Boulder", "Shake", "DROP"));
        Assert.False(LocalTraps.Echoes("bone_crumble_plat", "bone_plat_01_crumble", "Break", "CRUMBLE"));
        Assert.False(LocalTraps.Echoes("Control", "Bone Beast", "Idle", "STRIKE"));
    }

    // (0.3.21) Bone_12's jails broke in the hitter's game only: the other player's stayed locked, its Pilgrim
    // StaffWielder and Thief NPC Save never let out (probe_reveals, c-reveals-p2-2.log). Only the break is told: a hit's
    // Hit Pos Check cancels by the local hero's side, so hit counts could differ between games.
    [Fact]
    public void A_jail_door_tells_the_room_its_break_only() {
        Assert.Equal("BREAK", LocalTraps.Sets("Control", "jail_door (1)", "Idle", "Break"));
        Assert.Equal("BREAK", LocalTraps.Sets("Control", "jail_door (2)", "Hit", "Break"));
        Assert.Null(LocalTraps.Sets("Control", "jail_door (1)", "Idle", "Hit"));
        Assert.Null(LocalTraps.Sets("Control", "jail_door (1)", "Hit", "Idle"));
    }

    [Fact]
    public void Another_games_jail_break_breaks_a_door_still_shut() {
        Assert.True(LocalTraps.Echoes("Control", "jail_door (1)", "Idle", "BREAK"));
        Assert.True(LocalTraps.Echoes("Control", "jail_door (2)", "Hit", "BREAK"));
        Assert.False(LocalTraps.Echoes("Control", "jail_door (1)", "Break", "BREAK"));
    }

    // (0.3.21) Bone_10's Caravan Lech came out wounded in the game of the player back by the Spa Door only: each game's
    // Dialogue reads its own hero's entry door, the other player's Lech stood well (probe_lech, c-flyer-p2-3.log). No
    // event leads from Idle to its Wounded State, so the room is told the state itself, which a game sets directly.
    [Fact]
    public void The_caravan_lech_tells_the_room_its_wounded_state() {
        Assert.Equal("@Wounded State", LocalTraps.Sets("Dialogue", "Caravan Lech", "Wounded?", "Wounded State"));
        Assert.Null(LocalTraps.Sets("Dialogue", "Caravan Lech", "Init", "Idle"));
        Assert.True(LocalTraps.Echoes("Dialogue", "Caravan Lech", "Idle", "@Wounded State"));
        Assert.False(LocalTraps.Echoes("Dialogue", "Caravan Lech", "Wounded State", "@Wounded State"));
    }

    // marrow_reveals (c-mreveals-g13-p3-1.log): the doer's Lech was already in its Wounded State when TrapFollow first
    // looked at the room (its hero in position at once by the Spa Door), so no change was ever seen and nothing told
    [Fact]
    public void A_state_told_kind_first_seen_in_its_state_tells_the_room() {
        Assert.Equal("@Wounded State", LocalTraps.SeenSet("Dialogue", "Caravan Lech", "Wounded State"));
        Assert.Null(LocalTraps.SeenSet("Dialogue", "Caravan Lech", "Idle"));
        // a terrain object seen first broken is its saved state, not a setting off
        Assert.Null(LocalTraps.SeenSet("Control", "jail_door (1)", "Break"));
        Assert.Null(LocalTraps.SeenSet("Control", "explode_wall (3)", "Strike"));
    }

    [Fact]
    public void A_word_naming_a_state_is_set_directly() {
        Assert.Equal("Wounded State", LocalTraps.StateOf("@Wounded State"));
        Assert.Null(LocalTraps.StateOf("BREAK"));
    }

    // 0.3.21 Task 10 Step 2: the Lech's Wounded State keeps LECH_WOUNDED for 3 room changes (its SetStaticVariable),
    // which its Wounded? reads first on the next entry; the room word reached only the players in the room, so a player
    // who came in later saw the Lech well (known differences, cause 15). One word: a second ArenaFollow sent in the
    // same frame replaced the first (single data), and every game away from the room dropped a word for it
    [Fact]
    public void A_wound_that_lasts_rooms_keeps_its_static_in_every_game() {
        const string word = "Black Thread States Thread Only Variant/Normal World/Caravan/Caravan State Regular/Caravan Lech|@Wounded State";
        Assert.Equal(("LECH_WOUNDED", 3), LocalTraps.LastingOf(word));
        Assert.Null(LocalTraps.LastingOf("Caravan/Caravan Lech|BREAK"));
        Assert.Null(LocalTraps.LastingOf("Jail Scene 1/jail_door (1)|@Wounded State"));
        Assert.Null(LocalTraps.LastingOf("no bar"));
        Assert.True(LocalTraps.ToEveryone("Trap:" + word));
        Assert.False(LocalTraps.ToEveryone("Trap:Jail Scene 1/jail_door (1)|BREAK"));
        Assert.False(LocalTraps.ToEveryone("Bell:" + word));
    }
}
