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
}
