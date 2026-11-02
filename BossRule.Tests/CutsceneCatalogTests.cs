using System.IO;
using System.Linq;
using Xunit;

namespace BossRule.Tests;

public class CutsceneCatalogTests {
    private static readonly string Shipped = File.ReadAllText(Path.Combine("..", "..", "..", "..", "BossRule", "cutscenes.json"));

    [Fact]
    public void The_shipped_catalog_loads_every_cutscene_with_its_room_fsm_and_start() {
        var entries = CutsceneCatalog.Load(Shipped);
        Assert.Equal(28, entries.Count);
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
        Assert.Equal(new[] { "churchkeeper_intro", "greymoor_crash", "widow_intro", "shrine_silkspear",
                "shrine_swift_step", "shrine_silk_charge", "shrine_thread_storm", "shrine_cling_grip", "crest_beast",
                "crest_wanderer", "crest_reaper", "drifters_cloak", "charge_slash" },
            entries.Where(e => e.Follow).Select(e => e.Name));
        Assert.Equal(new[] { "End", null, "End", "Set Finished", "Set Finished", "Set Finished", "Set Finished",
                "Set Finished", "End 2", "End 2", "End 2", "Talk End 3", "Down To Idle" },
            entries.Where(e => e.Follow).Select(e => e.Release));
        Assert.Null(CutsceneCatalog.Load("""[{"name": "a", "scenes": ["R"], "object": "O", "fsm": "F", "start": ["S"]}]""")[0].Release);
    }

    [Fact]
    public void A_rooms_entries_are_those_naming_it() {
        var entries = CutsceneCatalog.Load(Shipped);
        Assert.Equal(new[] { "bellhart_arrival", "bellhart_web" }, CutsceneCatalog.InRoom(entries, "Belltown").Select(e => e.Name));
    }
}
