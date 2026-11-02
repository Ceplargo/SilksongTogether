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
    // game that runs her), a shrine's Set Finished (a follow past its End)
    [Fact]
    public void The_followed_cutscenes_name_the_state_by_which_a_held_hero_is_given_back() {
        var entries = CutsceneCatalog.Load(Shipped);
        Assert.Equal(new[] { "widow_intro", "shrine_silkspear", "shrine_swift_step", "shrine_silk_charge" },
            entries.Where(e => e.Follow).Select(e => e.Name));
        Assert.Equal(new[] { "End", "Set Finished", "Set Finished", "Set Finished" },
            entries.Where(e => e.Follow).Select(e => e.Release));
        Assert.Null(CutsceneCatalog.Load("""[{"name": "a", "scenes": ["R"], "object": "O", "fsm": "F", "start": ["S"]}]""")[0].Release);
    }

    [Fact]
    public void A_rooms_entries_are_those_naming_it() {
        var entries = CutsceneCatalog.Load(Shipped);
        Assert.Equal(new[] { "bellhart_arrival", "bellhart_web" }, CutsceneCatalog.InRoom(entries, "Belltown").Select(e => e.Name));
    }
}
