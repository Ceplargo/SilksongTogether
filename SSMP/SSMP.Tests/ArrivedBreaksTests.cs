using SSMP.Game.Client.Save;
using Xunit;

namespace SSMP.Tests;

public class ArrivedBreaksTests {
    // the host's copied hit broke Bone_07's deposit; the friend's, still in Idle, breaks as it arrives
    [Fact]
    public void An_arrived_break_breaks_an_idle_deposit() =>
        Assert.True(ArrivedBreaks.Sends("tool_metal_deposit", true, false, ["HIT", "BREAK"]));

    [Fact]
    public void A_deposit_already_broken_is_left() =>
        Assert.False(ArrivedBreaks.Sends("tool_metal_deposit", true, true, ["HIT", "BREAK"]));

    // a state that takes no BREAK (Hit Effects mid-hit takes one; Activated takes none): the variable alone
    [Fact]
    public void A_state_without_break_is_left() =>
        Assert.False(ArrivedBreaks.Sends("tool_metal_deposit", true, false, ["FINISHED"]));

    [Fact]
    public void An_arrived_unbroken_value_breaks_nothing() =>
        Assert.False(ArrivedBreaks.Sends("tool_metal_deposit", false, true, ["BREAK"]));

    // probe_chain_drop (2026-11-11_025520-pair2): BREAK in Idle is the chain slash's break, straight to Plat Fall,
    // skipping the landing's antic BossRule's trap follow gives it: left to that follow
    [Fact]
    public void An_arrived_break_leaves_the_chain_drop_platform() =>
        Assert.False(ArrivedBreaks.Sends("Chain Drop Platform", true, false, ["LAND", "BREAK"]));

    // Bone_01's Collapser Small: BREAK in Idle is its own sequence (Antic Type), as its trap follow sends it
    [Fact]
    public void An_arrived_break_breaks_an_idle_collapser() =>
        Assert.True(ArrivedBreaks.Sends("Collapser Small (1)", true, false, ["BREAK"]));
}
