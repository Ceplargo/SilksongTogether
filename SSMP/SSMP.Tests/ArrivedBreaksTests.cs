using SSMP.Game.Client.Save;
using Xunit;

namespace SSMP.Tests;

public class ArrivedBreaksTests {
    private static ArrivedBreak? Of(string name, bool value, bool before, string[] events, string fsm = "Control") =>
        ArrivedBreaks.Of(name, fsm, value, before, events);

    // the host's copied hit broke Bone_07's deposit; the friend's, still in Idle, breaks as it arrives
    [Fact]
    public void An_arrived_break_breaks_an_idle_deposit() =>
        Assert.Equal(new ArrivedBreak("BREAK", null), Of("tool_metal_deposit", true, false, ["HIT", "BREAK"]));

    [Fact]
    public void A_deposit_already_broken_is_left() =>
        Assert.Null(Of("tool_metal_deposit", true, true, ["HIT", "BREAK"]));

    // a state that takes no BREAK (Hit Effects mid-hit takes one; Activated takes none): the variable alone
    [Fact]
    public void A_state_without_break_is_left() =>
        Assert.Null(Of("tool_metal_deposit", true, false, ["FINISHED"]));

    [Fact]
    public void An_arrived_unbroken_value_breaks_nothing() =>
        Assert.Null(Of("tool_metal_deposit", false, true, ["BREAK"]));

    // probe_chain_drop (2026-11-11_025520-pair2): BREAK in Idle is the chain slash's break, straight to Plat Fall,
    // skipping the landing's antic BossRule's trap follow gives it: left to that follow
    [Fact]
    public void An_arrived_break_leaves_the_chain_drop_platform() =>
        Assert.Null(Of("Chain Drop Platform", true, false, ["LAND", "BREAK"]));

    // Bone_01's Collapser Small: BREAK in Idle is its own sequence (Antic Type), as its trap follow sends it
    [Fact]
    public void An_arrived_break_breaks_an_idle_collapser() =>
        Assert.Equal(new ArrivedBreak("BREAK", null), Of("Collapser Small (1)", true, false, ["BREAK"]));

    // (0.3.23) Greymoor_22's One Way Wall (2): Idle takes hits only, each checking this game's hero range; an arrived
    // break moves it into Break, its last hit's state
    [Fact]
    public void An_arrived_break_breaks_an_idle_one_way_wall() =>
        Assert.Equal(new ArrivedBreak(null, "Break"), Of("One Way Wall (2)", true, false, ["NAIL HIT", "HEAVY"], "break_floor"));

    [Fact]
    public void A_one_way_wall_mid_hit_is_left() =>
        Assert.Null(Of("One Way Wall (2)", true, false, ["FINISHED"], "break_floor"));

    [Fact]
    public void A_one_way_wall_already_broken_is_left() =>
        Assert.Null(Of("One Way Wall (2)", true, true, ["NAIL HIT", "HEAVY"], "break_floor"));

    // Bone_01's Breakable Wall: Idle takes the game's own QUICK BREAK
    [Fact]
    public void An_arrived_break_quick_breaks_an_idle_breakable_wall() =>
        Assert.Equal(new ArrivedBreak("QUICK BREAK", null),
            Of("Breakable Wall", true, false, ["WALL BREAKER", "NEEDLE", "QUICK BREAK"], "breakable_wall_v2"));

    // (0.4) Shellwood's Twig Walls: Idle takes hits, ACTIVATED (the load's) and the game's own DESTROY, which goes
    // PD Bool, Hit 4, End: the friend broke Shellwood_25's (2) with one copied hit lost, the host's stayed standing
    [Fact]
    public void An_arrived_break_destroys_an_idle_twig_wall() =>
        Assert.Equal(new ArrivedBreak("DESTROY", null), Of("Shellwood Twig Wall (2)", true, false, ["NAIL HIT", "DESTROY", "ACTIVATED"]));

    [Fact]
    public void A_twig_wall_mid_hit_is_left() =>
        Assert.Null(Of("Shellwood Twig Wall (2)", true, false, ["FINISHED"]));

    [Fact]
    public void Another_objects_destroy_is_left() =>
        Assert.Null(Of("Some Lamp", true, false, ["NAIL HIT", "DESTROY"]));

    // (0.4) a break that arrives mid-hit (Belltown_basement_03's One Way Wall (4) under lag 50) is tried again till
    // the object is back in a state that takes it
    [Fact]
    public void A_new_break_waits() => Assert.True(ArrivedBreaks.Waits(true, false));

    [Fact]
    public void An_old_or_unbroken_value_does_not_wait() {
        Assert.False(ArrivedBreaks.Waits(true, true));
        Assert.False(ArrivedBreaks.Waits(false, false));
    }

    // (0.4, stage3c) Under_27's two "Shellwood Twig Wall (1)" (the root's and Group (1)'s) save under one id: the host
    // broke one, its swing copied broke the same one in the friend's game, and the friend's value arrived at the host's
    // other one, still in Idle, which DESTROY opened (the game alone leaves it standing till a reload)
    [Fact]
    public void A_break_whose_id_two_live_objects_share_is_left() {
        Assert.Null(ArrivedBreaks.Of("Shellwood Twig Wall (1)", "Control", true, false, ["NAIL HIT", "DESTROY", "ACTIVATED"], 2));
        Assert.False(ArrivedBreaks.Waits(true, false, 2));
    }
}
