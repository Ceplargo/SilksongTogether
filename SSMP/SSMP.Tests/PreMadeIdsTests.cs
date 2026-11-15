using System.Collections.Generic;
using System.Linq;
using SSMP.Game.Client.Entity;
using Xunit;

namespace SSMP.Tests;

public class PreMadeIdsTests {
    private const string Scene = "Shellwood_01";

    // each corpse's gnat is made at the scene's root at the prefab's place: their own hierarchies are alike
    private static readonly (string, float, float, float)[] Gnat = [("Shellwood Gnat(Clone)", 46.77f, 6.58f, 0.01f)];

    private static (string, float, float, float)[] Maker(string enemy, float x, float y) =>
        [(enemy, x, y, 0f), ("Corpse Shellwood Goomba(Clone)", 0f, 0f, 0f)];

    private static ushort Id((string, float, float, float)[]? maker) => PreMadeIds.Hash(Scene, PreMadeIds.Chain(Gnat, maker));

    // the errors-file scan (NOTES 347): "Stable entity ID collision between 'Shellwood Gnat(Clone)' and ..."
    [Fact]
    public void Two_premade_objects_of_two_makers_get_two_ids() =>
        Assert.NotEqual(Id(Maker("Shellwood Goomba", 25.88f, 25.07f)), Id(Maker("Shellwood Goomba (2)", 29.43f, 52.95f)));

    // each game loads its corpses in its own order: a gnat's id comes from its maker alone
    [Fact]
    public void Each_games_ids_are_the_same_whatever_order_it_makes_them_in() {
        var makers = new[] {
            Maker("Shellwood Goomba", 25.88f, 25.07f), Maker("Pond Skater", 92.3f, 3.79f), Maker("Shellwood Goomba (2)", 29.43f, 52.95f)
        };
        Assert.Equal(makers.Select(Id).ToList(), makers.Reverse().Select(Id).Reverse().ToList());
    }

    // Review Focus 1: a gnat one game alone makes moves no other gnat's id
    [Fact]
    public void A_premade_object_one_game_alone_makes_moves_no_other_id() {
        var both = new[] { Maker("Shellwood Goomba", 25.88f, 25.07f), Maker("Pond Skater", 92.3f, 3.79f) };
        var before = both.Select(Id).ToList();
        var extra = Id(Maker("Pond Skater (3)", 35.94f, 3.79f));
        Assert.Equal(before, both.Select(Id).ToList());
        Assert.DoesNotContain(extra, before);
    }

    // a scene object (no maker) keeps the id the old hash gave it (pinned from that hash)
    [Fact]
    public void A_scene_objects_id_is_unchanged() {
        (string, float, float, float)[] goomba = [("Shellwood Goomba", 25.88f, 25.07f, 0f)];
        Assert.Equal(goomba, PreMadeIds.Chain(goomba, null));
        Assert.Equal(47803, PreMadeIds.Hash(Scene, goomba));
    }
}
