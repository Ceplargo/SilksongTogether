using BossRule;
using Xunit;

namespace BossRule.Tests;

public class TollRuleTests {
    // probe_toll (0.3.21): the host paid Bone_01c's toll; the friend's machine stayed in Inert with its Activated set by
    // the machine's saved value from the host, its bench down
    [Fact]
    public void A_waiting_machine_activated_by_the_payers_saved_value_follows() =>
        Assert.True(TollRule.PaidElsewhere("Inert", true));

    [Fact]
    public void An_unpaid_machine_waits() => Assert.False(TollRule.PaidElsewhere("Inert", false));

    [Fact]
    public void A_machine_paid_here_runs_its_own_states() =>
        Assert.False(TollRule.PaidElsewhere("Retract Animation", true));

    [Fact]
    public void A_machine_already_on_its_way_off_is_left_alone() =>
        Assert.False(TollRule.PaidElsewhere("After Retract Pause", true));

    // probe_toll_sounds (0.3.21): the payer's coins play in its machine's Unlock Behaviour (its Prompt's sub-FSM)
    [Fact]
    public void The_toll_machines_fsm_is_the_payment() =>
        Assert.True(TollRule.IsMachine("Unlock Behaviour", "bell_toll_machine"));

    [Fact]
    public void Another_objects_fsm_of_that_name_is_not() =>
        Assert.False(TollRule.IsMachine("Unlock Behaviour", "bell_bench"));

    [Fact]
    public void Another_fsm_of_the_machine_is_not() => Assert.False(TollRule.IsMachine("Control", "bell_toll_machine"));

    [Fact]
    public void A_machine_unpaid_here_plays_the_coins_paid_in_another_game() =>
        Assert.True(TollRule.PlaysCoinsHere("Inert", false));

    [Fact]
    public void A_machine_whose_payment_already_came_does_not() => Assert.False(TollRule.PlaysCoinsHere("Inert", true));

    [Fact]
    public void A_machine_this_player_is_paying_at_plays_its_own() =>
        Assert.False(TollRule.PlaysCoinsHere("Prompt", false));
}
