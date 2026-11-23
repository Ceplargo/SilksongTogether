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

    // (0.4 enemies, NOTES 498) freeze_catch's try that came 0.62 s late: the print says how long ago the box was out
    [Fact]
    public void Since_is_the_time_from_the_named_box_last_out() {
        var late = new LateCatches<string>();
        Assert.Null(late.Since("Spinner Boss/NewCharge Hit", 10f));
        late.Saw(Box, 10f, ["early"]);
        late.Saw(Box, 11f, ["late"]);
        Assert.Equal(0.5f, late.Since("Spinner Boss/NewCharge Hit", 11.5f)!.Value, 3);
        Assert.Null(late.Since("Spinner Boss/DashSlash Hit", 11.5f));
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
public class SlashTiesTests {
    private const string Box = "Black Thread States Thread Only Variant/Normal World/Boss Scene/Spinner Boss/NewCharge Hit";
    private const string Named = "Spinner Boss/NewCharge Hit";

    private static (SlashTies Host, SlashTies Follower) Both() => (new SlashTies(), new SlashTies());

    private static void Out(SlashTies host, SlashTies follower, float at) {
        var word = SlashTies.ReadWord(SlashTies.Word("Host", Box, host.Rose(Box)))!.Value;
        follower.Heard(word.Host, word.Path, word.Slash, at);
    }

    [Fact]
    public void A_catch_of_the_boxs_latest_slash_is_taken() {
        var (host, follower) = Both();
        Out(host, follower, 10f);
        var (message, by, slash) = SlashTies.Untie("Friend|" + Named + follower.Tie(Named, 10.3f));
        Assert.Equal("Friend|" + Named, message);
        Assert.True(host.Same(Named, "Host", by, slash));
    }

    // NOTES 346: a catch held 3.9 s was honoured on her next slash
    [Fact]
    public void A_catch_of_an_earlier_slash_is_stale() {
        var (host, follower) = Both();
        Out(host, follower, 10f);
        var tie = follower.Tie(Named, 10.3f);
        Out(host, follower, 14f);
        var (_, by, slash) = SlashTies.Untie("Friend|" + Named + tie);
        Assert.False(host.Same(Named, "Host", by, slash));
    }

    // the box's new slash came out in the following game before its word did (one send behind)
    [Fact]
    public void A_catch_long_after_the_last_word_is_the_next_slashs() {
        var (host, follower) = Both();
        Out(host, follower, 10f);
        host.Rose(Box);
        var (_, by, slash) = SlashTies.Untie("Friend|" + Named + follower.Tie(Named, 10f + SlashTies.OutS + 2f));
        Assert.Equal(2, slash);
        Assert.True(host.Same(Named, "Host", by, slash));
    }

    [Fact]
    public void Another_scene_hosts_numbers_and_an_untied_catch_are_not_judged() {
        var (host, follower) = Both();
        Out(host, follower, 10f);
        var (_, by, slash) = SlashTies.Untie("Friend|" + Named + follower.Tie(Named, 10.3f));
        Assert.Null(host.Same(Named, "New Host", by, slash));
        var untied = SlashTies.Untie("Friend|" + Named + new SlashTies().Tie(Named, 10.3f));
        Assert.Equal(("Friend|" + Named, (string?) null, 0), untied);
        Assert.Null(host.Same(Named, "Host", untied.Host, untied.Slash));
    }

    // 0.4 enemies Task 6's in-place run (2026-11-17_234621-pair2): Vampire Gnat Scene's box rose once, then Vampire
    // Gnat Boss Scene's box of the same name ten times; the scene host judged slash 10 against the first box's 1
    private const string Other = "Other Scene/Spinner Boss/NewCharge Hit";

    [Fact]
    public void Of_two_boxes_of_one_name_the_scene_host_judges_the_latest_risen() {
        var host = new SlashTies();
        host.Rose(Other);
        for (var i = 0; i < 10; i++) host.Rose(Box);
        Assert.Equal(10, host.Latest(Named));
        Assert.True(host.Same(Named, "Host", "Host", 10));
    }

    [Fact]
    public void Of_two_boxes_of_one_name_a_following_game_ties_to_the_latest_heard() {
        var follower = new SlashTies();
        follower.Heard("Host", Other, 1, 10f);
        follower.Heard("Host", Box, 10, 20f);
        Assert.Equal(10, SlashTies.Untie("Friend|" + Named + follower.Tie(Named, 20.3f)).Slash);
    }

    [Fact]
    public void Numbers_heard_in_a_room_left_are_no_more() {
        var (host, follower) = Both();
        Out(host, follower, 10f);
        follower.ClearHeard();
        Assert.Equal("", follower.Tie(Named, 10.3f));
    }

    [Fact]
    public void Another_box_has_its_own_numbers() {
        var (host, follower) = Both();
        Out(host, follower, 10f);
        host.Rose(Box.Replace("NewCharge Hit", "DashSlash Hit"));
        var (_, by, slash) = SlashTies.Untie("Friend|" + Named + follower.Tie(Named, 10.3f));
        Assert.True(host.Same(Named, "Host", by, slash));
        Assert.Equal("", follower.Tie("Spinner Boss/DashSlash Hit", 10.3f));
    }
}
