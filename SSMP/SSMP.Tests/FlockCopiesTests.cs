using System.Collections.Generic;
using System.Linq;
using SSMP.Game.Client.Entity;
using Xunit;

namespace SSMP.Tests;

public class FlockCopiesTests {
    private static readonly (string Name, int Sibling)[] Path = {
        ("Black Thread States Thread Only Variant", 1), ("Normal World", 0), ("Fields Flock Flyer (6)", 5)
    };

    [Fact]
    public void Identity_uses_the_authored_path_not_random_position_or_discovery_order() {
        var ids = Enumerable.Range(0, 18).ToDictionary(i => i,
            i => FlockCopies.Id("Bone_East_02", Path.Take(2).Append(("Fields Flock Flyer", i))));
        Assert.Equal(18, ids.Values.Distinct().Count());
        foreach (var i in Enumerable.Range(0, 18).Reverse())
            Assert.Equal(ids[i], FlockCopies.Id("Bone_East_02", Path.Take(2).Append(("Fields Flock Flyer", i))));
    }

    [Fact]
    public void Same_name_at_different_siblings_does_not_merge_birds() =>
        Assert.NotEqual(FlockCopies.Id("Bone_East_02", Path),
            FlockCopies.Id("Bone_East_02", Path.Take(2).Append((Path[2].Name, 6))));

    [Fact]
    public void Different_world_parents_do_not_merge_birds() =>
        Assert.NotEqual(FlockCopies.Id("Bone_East_02", Path),
            FlockCopies.Id("Bone_East_02", new[] { Path[0], ("Black Thread World", 1), Path[2] }));

    [Fact]
    public void Different_rooms_do_not_share_identity() =>
        Assert.NotEqual(FlockCopies.Id("Bone_East_02", Path), FlockCopies.Id("Bone_East_14b", Path));

    [Theory]
    [InlineData("Fields Flock Flyer Tall Anim", 0)]
    [InlineData("Fields Flock Flyer Short Anim", 1)]
    [InlineData("unknown", -1)]
    public void Received_appearance_selects_a_local_asset_by_name(string name, int expected) =>
        Assert.Equal(expected, FlockCopies.AppearanceIndex(
            new[] { "Fields Flock Flyer Tall Anim", "Fields Flock Flyer Short Anim" }, name));

    [Theory]
    [InlineData("Fly", true)]
    [InlineData("Fly Antic", true)]
    [InlineData("Idle", false)]
    [InlineData("", false)]
    public void Handed_over_flight_restarts_only_a_flying_bird(string clip, bool expected) =>
        Assert.Equal(expected, FlockCopies.ResumesFlight(clip));

    [Fact]
    public void Name_boundaries_are_part_of_the_identity() =>
        Assert.NotEqual(FlockCopies.Id("A", new[] { ("BC", 0) }), FlockCopies.Id("AB", new[] { ("C", 0) }));
}
