using System.IO;
using System.Linq;
using Xunit;

namespace BossRule.Tests;

public class CutsceneCatalogTests {
    private static readonly string Shipped = File.ReadAllText(Path.Combine("..", "..", "..", "..", "BossRule", "cutscenes.json"));

    [Fact]
    public void The_shipped_catalog_loads_every_cutscene_with_its_room_fsm_and_start() {
        var entries = CutsceneCatalog.Load(Shipped);
        Assert.Equal(30, entries.Count);  // 28 cutscenes, the Bell Beast's heart's memory part and the Widow's (Memory_Needolin) (0.4 world Task 17; no Fourth Chorus heart: the game has no Memory_Silk_Heart_SongGolem)
        Assert.Equal(entries.Count, entries.Select(e => e.Name).Distinct().Count());
        Assert.All(entries, e => Assert.True(e.Scenes.Count > 0 && e.Object != "" && e.Fsm != "" && e.Start.Count > 0, e.Name));
    }

    [Fact]
    public void An_entry_is_followed_unless_it_says_not() {
        var entries = CutsceneCatalog.Load(
            """[{"name": "a", "scenes": ["R"], "object": "O", "fsm": "F", "start": ["S"]},""" +
            """ {"name": "b", "scenes": ["R"], "object": "O", "fsm": "F", "start": ["S"], "follow": false}]""");
        Assert.Equal(new[] { true, false }, entries.Select(e => e.Follow));
    }

    // Followed are the cutscenes proven in all four cases (bellhart_web is a wake-up, each player's own); each names the
    // state by which a hero nothing else gives back takes input again: the Widow's talk's End (her roar runs only in the
    // game that runs her), a shrine's Set Finished (a follow past its End); none for Greymoor's crash, whose
    // landing in Greymoor_01 gives each game's hero back; the Churchkeeper's End (a follower pressing on first);
    // the Seamstress's Talk End 3 and the Pinstress's Down To Idle, after their videos; a crest's End 2
    // a hit on a crest's setter at its first stab ends its interaction (CONVO_END_FORCED) and the FSM goes to Cancel,
    // giving only the setter back: a follower held since the follow's start is given back there
    [Fact]
    public void A_crest_cancelled_by_a_hit_gives_its_follower_back_at_Cancel() {
        Assert.Equal("Cancel", CutsceneCatalog.Load(Shipped).Single(e => e.Name == "crest_reaper").Cancel);
        var bare = CutsceneCatalog.Load(
            """[{"name": "a", "scenes": ["R"], "object": "O", "fsm": "F", "start": ["S"]}]""");
        Assert.Null(bare[0].Cancel);
    }

    [Fact]
    public void A_crest_never_follows_into_its_memory() {
        Assert.Equal(new[] { "To Memory" }, CutsceneCatalog.Load(Shipped).Single(e => e.Name == "crest_reaper").Skip!);
        var bare = CutsceneCatalog.Load(
            """[{"name": "a", "scenes": ["R"], "object": "O", "fsm": "F", "start": ["S"]}]""");
        Assert.Empty(bare[0].Skip!);
    }

    [Fact]
    public void The_followed_cutscenes_name_the_state_by_which_a_held_hero_is_given_back() {
        var entries = CutsceneCatalog.Load(Shipped);
        Assert.Equal(new[] { "churchkeeper_intro", "greymoor_crash", "widow_intro", "widow_win", "widow_win/memory", "silk_heart_bellbeast",
                "silk_heart_bellbeast/memory", "shrine_silkspear",
                "shrine_swift_step", "shrine_silk_charge", "shrine_thread_storm", "shrine_cling_grip", "crest_beast",
                "crest_wanderer", "crest_reaper", "drifters_cloak", "charge_slash", "needolin_deep_shellwood", "bellway_ride",
                "caravan_ride" },
            entries.Where(e => e.Follow).Select(e => e.Name));
        // (0.4 world Task 17) the Bell Beast's heart and its memory: each game's own Silk Heart Memory Return gives
        // its hero back; Shellwood_11b's deep memory (0.4 world Task 17 Step 4): the Flower Queen's own death in each
        // game (her corpse's Heart Death) brings its hero back
        Assert.Equal(new[] { "End", null, "End", null, null, null, null, "Set Finished", "Set Finished", "Set Finished", "Set Finished",
                "Set Finished", "End 2", "End 2", "End 2", "Talk End 3", "Down To Idle", null, null, null },
            entries.Where(e => e.Follow).Select(e => e.Release));
        Assert.Null(CutsceneCatalog.Load("""[{"name": "a", "scenes": ["R"], "object": "O", "fsm": "F", "start": ["S"]}]""")[0].Release);
    }

    // (0.4 world Task 17 Step 4) The Widow's win: in the friend's game her FSM is SSMP's copy, off, so it follows none
    // of her states (widow-follow-p2: "is off: Hornet Dash not followed"); it goes into Memory_Needolin itself as hers
    // reaches To Memory Scene, and her memory's Memory Control is followed there
    [Fact]
    public void The_widows_win_enters_her_memory_in_a_game_whose_fsm_is_off() {
        var widow = CutsceneCatalog.Load(Shipped).Single(e => e.Name == "widow_win");
        Assert.Equal(new CutsceneEnter("To Memory Scene", "Memory_Needolin", "door_wakeOnGround"), widow.Enter);
        Assert.True(CutsceneEnter.Due(widow.Enter, "To Memory Scene", fsmOn: false));
        Assert.False(CutsceneEnter.Due(widow.Enter, "To Memory Scene", fsmOn: true));  // a game that runs her plays it itself
        Assert.False(CutsceneEnter.Due(widow.Enter, "Get Needolin", fsmOn: false));
        Assert.False(CutsceneEnter.Due(null, "To Memory Scene", fsmOn: false));
    }

    // (0.3.21) A ride is its rider's: the other game shows what the room shows (the Bell Beast coming; the caravan
    // leaving) and keeps its own hero, never following the rider's own states (the map, the fling, the room change)
    [Fact]
    public void A_ride_is_followed_without_the_followers_hero() {
        var entries = CutsceneCatalog.Load(Shipped);
        Assert.Equal(new[] { "bellway_ride", "caravan_ride" }, entries.Where(e => e.KeepsHero).Select(e => e.Name));
        var beast = entries.Single(e => e.Name == "bellway_ride");
        Assert.Equal(new[] { "Appear Delay" }, beast.Start);
        Assert.Subset(beast.Skip!.ToHashSet(), new[] { "Open map", "Fade", "Hero Jump", "Hero Fire", "Jump Sing",
            "Time Passes", "Go To Stag Cutscene" }.ToHashSet());
        Assert.Empty(beast.LeaveAt!);
        Assert.Equal(new[] { "Sound With", "Sound Without" }, entries.Single(e => e.Name == "caravan_ride").LeaveAt);
        var bare = CutsceneCatalog.Load(
            """[{"name": "a", "scenes": ["R"], "object": "O", "fsm": "F", "start": ["S"]}]""");
        Assert.False(bare[0].KeepsHero);
        Assert.Empty(bare[0].LeaveAt!);
    }

    [Fact]
    public void A_rooms_entries_are_those_naming_it() {
        var entries = CutsceneCatalog.Load(Shipped);
        Assert.Equal(new[] { "bellhart_arrival", "bellhart_web" }, CutsceneCatalog.InRoom(entries, "Belltown").Select(e => e.Name));
    }
}
