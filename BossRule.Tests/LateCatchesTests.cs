using Xunit;

namespace BossRule.Tests;

public class LateCatchesTests {
    private const string Box = "Black Thread States Thread Only Variant/Normal World/Boss Scene/Spinner Boss/NewCharge Hit";

    [Fact]
    public void A_catch_just_after_its_box_went_takes_her_where_the_box_would_have() {
        var late = new LateCatches<string>();
        late.Saw(Box, 10f, ["Control to Multihit Slash"]);
        Assert.Equal(["Control to Multihit Slash"], late.For("Spinner Boss/NewCharge Hit", 10.2f));
    }

    [Fact]
    public void A_catch_later_than_the_keep_is_dropped() {
        var late = new LateCatches<string>();
        late.Saw(Box, 10f, ["Control to Multihit Slash"]);
        Assert.Null(late.For("Spinner Boss/NewCharge Hit", 10f + LateCatches<string>.KeepS + 0.01f));
    }

    [Fact]
    public void Another_box_is_not_taken_for_it() {
        var late = new LateCatches<string>();
        late.Saw(Box, 10f, ["Control to Multihit Slash"]);
        Assert.Null(late.For("Spinner Boss/DashSlash Hit", 10.1f));
    }

    [Fact]
    public void The_last_sighting_counts() {
        var late = new LateCatches<string>();
        late.Saw(Box, 10f, ["early"]);
        late.Saw(Box, 11f, ["late"]);
        Assert.Equal(["late"], late.For("Spinner Boss/NewCharge Hit", 11.1f));
    }

    // The whole-branch review (2026-11-05): with two caught at once, the scene host's own catch put her in her multi
    // slash and the other's late one started it again, aimed at the other.
    [Fact]
    public void A_box_that_took_its_connect_here_is_not_taken_again() {
        var late = new LateCatches<string>();
        late.Saw(Box, 10f, ["Control to Multihit Slash"]);
        late.Took(Box, 10.05f);
        Assert.Null(late.For("Spinner Boss/NewCharge Hit", 10.2f));
    }

    [Fact]
    public void A_late_catch_is_honoured_once() {
        var late = new LateCatches<string>();
        late.Saw(Box, 10f, ["Control to Multihit Slash"]);
        Assert.NotNull(late.For("Spinner Boss/NewCharge Hit", 10.1f));
        Assert.Null(late.For("Spinner Boss/NewCharge Hit", 10.15f));
    }

    [Fact]
    public void Without_using_up_every_late_catch_is_honoured_as_before() {
        var late = new LateCatches<string>();
        late.Saw(Box, 10f, ["Control to Multihit Slash"]);
        Assert.NotNull(late.For("Spinner Boss/NewCharge Hit", 10.1f, usesUp: false));
        Assert.NotNull(late.For("Spinner Boss/NewCharge Hit", 10.15f, usesUp: false));
    }

    [Fact]
    public void A_connect_taken_before_the_box_was_seen_again_does_not_count() {
        var late = new LateCatches<string>();
        late.Took(Box, 5f);
        late.Saw(Box, 10f, ["Control to Multihit Slash"]);
        Assert.Equal(["Control to Multihit Slash"], late.For("Spinner Boss/NewCharge Hit", 10.2f));
    }

    [Fact]
    public void Forgotten_on_a_new_room() {
        var late = new LateCatches<string>();
        late.Saw(Box, 10f, ["Control to Multihit Slash"]);
        late.Clear();
        Assert.Null(late.For("Spinner Boss/NewCharge Hit", 10.1f));
    }
}
