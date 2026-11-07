using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace BossRule.Tests;

public class BossCatalogTests {
    private const string SampleJson = """
        [{"flag": "defeatedMossMother", "scene": "Mosstown_03", "gate": "left1", "boss": "Moss Mother",
          "start": {"kind": "fsm", "object": "Moss Mother", "fsm": "Control", "state": "Idle", "event": "WAKE"},
          "area": {"kind": "trigger", "object": "Wake Trigger"},
          "needs": {"hasNeedolin": true}, "hp": 1000,
          "phases": [{"object": "Moss Mother", "fsm": "Control", "var": "Rage HP", "value": 500}]}]
        """;

    [Fact]
    public void A_start_matches_each_listed_state_and_event_and_no_part_of_one() {
        var start = new BossStart("fsm", "Boss Scene", "Control", "Idle|Wait", "ENTER|EARLY ENTER");
        Assert.True(start.StartsOn("Idle", "ENTER"));
        Assert.True(start.StartsOn("Wait", "EARLY ENTER"));
        Assert.False(start.StartsOn("Idle", "EARLY"));
        Assert.False(start.StartsOn("Id", "ENTER"));
        Assert.False(start.StartsOn("Run", "ENTER"));
        Assert.False(new BossStart("battleScene", "Battle Scene", null, null, null).StartsOn("Idle", "ENTER"));
    }

    [Fact]
    public void A_start_match_allocates_nothing() {
        // StartGate.ArmOnStart asks it for every FSM event until a gate is armed in a boss room
        var start = new BossStart("fsm", "Boss Scene", "Control", "Idle|Wait", "ENTER|EARLY ENTER");
        start.StartsOn("Wait", "EARLY ENTER");
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++) start.StartsOn("Wait", "EARLY ENTER");
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void Catalog_loads_start_and_area() {
        var e = BossCatalog.Load(SampleJson)[0];
        Assert.Equal(("fsm", "Control", "Idle", "WAKE"), (e.Start.Kind, e.Start.Fsm, e.Start.State, e.Start.Event));
        Assert.Equal("trigger", e.Area.Kind);
        Assert.Equal((1000, 500f), (e.Hp, e.Phases[0].Value));
    }

    [Fact]
    public void Catalog_loads_what_the_boss_needs_and_where_it_is() {
        var e = BossCatalog.Load(SampleJson)[0];
        Assert.Equal(("defeatedMossMother", "Mosstown_03", "Moss Mother"), (e.Flag, e.Scene, e.Boss));
        Assert.True(e.Needs["hasNeedolin"]);
        Assert.Equal("Wake Trigger", e.Area.Object);
    }

    // The Last Judge's refight: her room's own FSM switches Gate L on at her start, the way onward from right1, so the
    // catalog names it for a held fight to shut (round three's walk-past sweep)
    [Fact]
    public void Catalog_loads_what_a_held_fight_shuts_and_from_which_doors() {
        var onward = BossCatalog.Load(SampleJson.Replace("\"hp\":",
            "\"onward\": [{\"object\": \"Boss Scene/Gate L\", \"from\": [\"right1\"]}], \"hp\":"))[0].Onward!;
        Assert.Equal("Boss Scene/Gate L", onward[0].Object);
        Assert.Equal(new[] { "right1" }, onward[0].From);
        Assert.Empty(BossCatalog.Load(SampleJson)[0].Onward!);
    }

    [Fact]
    public void A_phase_can_be_a_fixed_number_in_an_action() {
        var e = BossCatalog.Load("""
            [{"flag": "f", "scene": "S", "gate": "g", "boss": "SG_head", "start": {"kind": "battleScene", "object": "B"},
              "area": {"kind": "room"}, "needs": {}, "hp": 500,
              "phases": [{"object": "SG_head", "fsm": "Phase Control", "state": "HP Check 1", "action": "CompareHP",
                          "field": "integer2", "value": 451}]}]
            """)[0];
        var phase = e.Phases[0];
        Assert.Equal((null, "HP Check 1", "CompareHP", "integer2", 451f),
            (phase.Var, phase.State, phase.Action, phase.Field, phase.Value));
    }

    [Fact]
    public void Room_area_and_battle_scene_start_need_no_fsm() {
        var e = BossCatalog.Load("""
            [{"flag": "f", "scene": "S", "gate": "g", "boss": "B", "start": {"kind": "battleScene", "object": "Battle Scene"},
              "area": {"kind": "room"}, "needs": {}, "hp": 5, "phases": []}]
            """)[0];
        Assert.Equal(("battleScene", "Battle Scene", null), (e.Start.Kind, e.Start.Object, e.Start.Fsm));
        Assert.Equal(("room", null), (e.Area.Kind, e.Area.Object));
        Assert.Empty(e.Phases);
    }

    [Fact]
    public void A_follow_names_the_states_whose_sends_the_others_repeat() {
        var e = BossCatalog.Load(FollowJson(", \"echo\": \"NPCs Start Singing|Needolin End\""))[0];
        Assert.Equal(["NPCs Start Singing", "Needolin End"], e.Follow!.Echo);
    }

    [Fact]
    public void A_follow_without_echo_repeats_nothing() => Assert.Empty(BossCatalog.Load(FollowJson(""))[0].Follow!.Echo);

    [Fact]
    public void An_echo_with_a_next_state_is_sent_only_on_that_move() {
        var follow = new BossFollow("Intro Flare", "In Range", ["NPCs Start Singing>NPCs Singing Wait", "Needolin End"]);
        Assert.Equal("NPCs Start Singing", follow.EchoOn("NPCs Start Singing", "NPCs Singing Wait"));
        // her bells not all rung: the state cancels to Wait For Cancel, and the host's pilgrims stay silent
        Assert.Null(follow.EchoOn("NPCs Start Singing", "Wait For Cancel"));
        Assert.Null(follow.EchoOn("In Range", "NPCs Start Singing"));
        Assert.Equal("Needolin End", follow.EchoOn("Start Gate", "Needolin End"));
        Assert.True(follow.Echoes("NPCs Start Singing"));
        Assert.False(follow.Echoes("NPCs Singing Wait"));
    }

    private static string FollowJson(string echo) =>
        "[{\"flag\": \"f\", \"scene\": \"S\", \"gate\": \"g\", \"boss\": \"B\", " +
        "\"start\": {\"kind\": \"fsm\", \"object\": \"Boss Scene\", \"fsm\": \"Control\", \"state\": \"Out\", \"event\": \"ENTER\"}, " +
        "\"area\": {\"kind\": \"room\"}, \"needs\": {}, \"hp\": 5, \"phases\": [], " +
        $"\"follow\": {{\"state\": \"Intro Flare\", \"from\": \"In Range\"{echo}}}}}]";

    [Fact]
    public void Entry_without_a_start_is_refused() =>
        Assert.Throws<FormatException>(() => BossCatalog.Load("""[{"flag": "f", "scene": "S", "hp": 5}]"""));

    private static BossEntry Room(string flag, string scene, params (string Name, bool Value)[] needs) =>
        new(flag, scene, "left1", "Boss", new BossStart("fsm", "Boss", "Control", "Idle", "GO"), new BossArea("room", null),
            needs.ToDictionary(n => n.Name, n => n.Value), 100, new List<BossPhase>());

    // The three rooms that hold two catalog entries, in the catalog's order
    private static readonly BossEntry[] SharedRooms = {
        Room("defeatedBoneFlyerGiantGolemScene", "Bone_East_08"),
        Room("defeatedSongGolem", "Bone_East_08", ("hasBrolly", true)),
        Room("defeatedTormentedTrobbio", "Library_13", ("blackThreadWorld", true)),
        Room("defeatedTrobbio", "Library_13"),
        Room("skullKingDefeated", "Bone_15"),
        Room("skullKingDefeatedBlackThreaded", "Bone_15", ("blackThreadWorld", true))
    };

    private static Func<string, bool> TrueOnly(params string[] names) => name => names.Contains(name);

    private static string[] Candidates(string scene, Func<string, bool> playerData) =>
        BossCatalog.Candidates(SharedRooms, scene, playerData).Select(e => e.Flag).ToArray();

    [Fact]
    public void Before_the_black_thread_the_Trobbio_room_holds_Trobbio() =>
        Assert.Equal(new[] { "defeatedTrobbio" }, Candidates("Library_13", TrueOnly()));

    [Fact]
    public void A_variant_whose_needs_hold_comes_before_the_plain_fight() {
        Assert.Equal(new[] { "defeatedTormentedTrobbio", "defeatedTrobbio" }, Candidates("Library_13", TrueOnly("blackThreadWorld")));
        Assert.Equal(new[] { "skullKingDefeatedBlackThreaded", "skullKingDefeated" }, Candidates("Bone_15", TrueOnly("blackThreadWorld")));
    }

    [Fact]
    public void The_Song_Golem_comes_first_and_the_rematch_is_still_a_candidate() =>
        Assert.Equal(new[] { "defeatedSongGolem", "defeatedBoneFlyerGiantGolemScene" }, Candidates("Bone_East_08", TrueOnly("hasBrolly")));

    [Fact]
    public void A_beaten_boss_is_no_candidate() =>
        Assert.Equal(new[] { "skullKingDefeated" },
            Candidates("Bone_15", TrueOnly("blackThreadWorld", "skullKingDefeatedBlackThreaded")));

    // The Dock Foremen: two bodies, and the room gives each its own health (the game's own value can differ: the Clover
    // Dancers' room sets 580 where the game's starting value is 300)
    [Fact]
    public void Each_body_scales_from_the_health_its_room_gives_it() {
        var e = BossCatalog.Load("""
            [{"flag": "defeatedDockForemen", "scene": "Dock_09", "gate": "left1", "boss": "Dock Guard Slasher|Dock Guard Thrower",
              "start": {"kind": "fsm", "object": "Boss Scene", "fsm": "Control", "state": "Battle Ready", "event": "ENTER"},
              "area": {"kind": "room"}, "needs": {}, "hp": 720, "bodyHp": {"Dock Guard Thrower": 520}, "phases": []}]
            """)[0];
        Assert.Equal((720, 520), (e.BaseHp("Dock Guard Slasher"), e.BaseHp("Dock Guard Thrower")));
    }

    // The Last Judge's first fight: her intro (the Needolin, the bell locks, her leap) runs in the scene host's game
    // only, so the other games' arenas, still In Range, follow it to Intro Flare
    [Fact]
    public void An_arena_with_a_long_intro_names_where_the_others_follow_it() {
        var e = BossCatalog.Load("""
            [{"flag": "defeatedLastJudge", "scene": "Coral_Judge_Arena", "gate": "right1", "boss": "Last Judge",
              "start": {"kind": "fsm", "object": "Boss Scene", "fsm": "Control", "state": "Out of Range|Encountered", "event": "ENTER"},
              "area": {"kind": "trigger", "object": "Needolin Range|Refight Range"}, "needs": {}, "hp": 720, "phases": [],
              "follow": {"state": "Intro Flare", "from": "In Range"}}]
            """)[0];
        Assert.NotNull(e.Follow);
        Assert.Equal(("Intro Flare", "In Range"), (e.Follow!.State, e.Follow.From));
        Assert.Null(BossCatalog.Load(SampleJson)[0].Follow);
    }

    // The Widow's room ends on its Boss Scene's own state, which only the game whose player rang her shrine reaches: a
    // game still waiting before it goes there too
    [Fact]
    public void A_room_that_ends_on_its_own_state_names_it_and_the_states_before_it() {
        var e = BossCatalog.Load("""
            [{"flag": "spinnerDefeated", "scene": "Belltown_Shrine", "gate": "right1", "boss": "Spinner Boss",
              "start": {"kind": "fsm", "object": "Spinner Boss", "fsm": "Control", "state": "Dormant", "event": "BATTLE START"},
              "area": {"kind": "room"}, "needs": {}, "hp": 360, "phases": [],
              "end": {"object": "Boss Scene", "fsm": "Control", "state": "Set Sequence Completed", "from": "Speak Ready|Defeated"}}]
            """)[0];
        Assert.Equal(("Boss Scene", "Control", "Set Sequence Completed"), (e.End!.Object, e.End.Fsm, e.End.State));
        Assert.Equal(["Speak Ready", "Defeated"], e.End.From);
        Assert.Null(BossCatalog.Load(SampleJson)[0].End);
    }

    // The Widow's talk takes the talker's hero and only her own roar gives it back, which runs in the scene host's game
    // only: a game that does not run her gives its hero back once her fight starts there
    [Fact]
    public void A_boss_whose_start_leaves_the_hero_held_says_so_and_only_the_widow_does() {
        var e = BossCatalog.Load("""
            [{"flag": "spinnerDefeated", "scene": "Belltown_Shrine", "gate": "right1", "boss": "Spinner Boss",
              "start": {"kind": "fsm", "object": "Spinner Boss", "fsm": "Control", "state": "Dormant", "event": "BATTLE START"},
              "area": {"kind": "room"}, "needs": {}, "hp": 360, "phases": [], "release": true}]
            """)[0];
        Assert.True(e.Release);
        Assert.False(BossCatalog.Load(SampleJson)[0].Release);
        var shipped = BossCatalog.Load(File.ReadAllText(Path.Combine("..", "..", "..", "..", "BossRule", "bosses.json")));
        Assert.Equal(["spinnerDefeated"], shipped.Where(b => b.Release).Select(b => b.Flag));
    }

    [Fact]
    public void The_Ant_Trappers_arena_follows_the_scene_hosts_into_Enter_Pause() {
        // (coop) on a first meeting the other games' arenas waited in Idle for their own hero under the lid it removes,
        // so their Unencountered Floor stayed (bosses Task 19, NOTES 327)
        var shipped = BossCatalog.Load(File.ReadAllText(Path.Combine("..", "..", "..", "..", "BossRule", "bosses.json")));
        var follow = shipped.Single(b => b.Flag == "defeatedAntTrapper").Follow;
        Assert.NotNull(follow);
        Assert.Equal("Enter Pause", follow!.State);
        Assert.Equal(["Idle", "Wait For Bench Break"], follow.From.Split('|'));
    }
}
