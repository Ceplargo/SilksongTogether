using SSMP.Game.Client.Entity.Component;
using Xunit;

namespace SSMP.Tests;

public class StageDamageTests {
    [Fact]
    public void Each_damager_is_one_byte_in_order() {
        // a Skull King boulder falling: its picked sprite at 2 (Drop's SetDamageHeroAmount), the other three at 0
        var amounts = StageDamage.Pack([0, 2, 0, 0, 2]);
        Assert.Equal(new byte[] { 0, 2, 0, 0, 2 }, amounts);
        Assert.Equal(2, StageDamage.Of(amounts, 1));
        Assert.Equal(0, StageDamage.Of(amounts, 0));
    }

    [Fact]
    public void An_amount_outside_a_byte_is_kept_inside_it() {
        Assert.Equal(new byte[] { 255, 0 }, StageDamage.Pack([300, -1]));
    }

    [Fact]
    public void A_damager_the_update_does_not_carry_is_left_as_it_is() {
        var amounts = StageDamage.Pack([2]);
        Assert.Null(StageDamage.Of(amounts, 1));
        Assert.Null(StageDamage.Of([], 0));
    }

    [Fact]
    public void A_copy_with_another_count_of_damagers_takes_none() {
        Assert.True(StageDamage.Pairs(5, 5));
        Assert.False(StageDamage.Pairs(5, 4));
        Assert.False(StageDamage.Pairs(0, 4)); // Sends off: none sent
        Assert.True(StageDamage.Pairs(StageDamage.Max, StageDamage.Max + 3));
    }

    [Fact]
    public void Damagers_past_the_most_an_update_carries_are_left_out() {
        var amounts = StageDamage.Pack(new int[StageDamage.Max + 1]);
        Assert.Equal(StageDamage.Max, amounts.Length);
    }
}
