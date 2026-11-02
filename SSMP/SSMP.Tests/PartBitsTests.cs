using SSMP.Game.Client.Entity.Component;
using Xunit;

namespace SSMP.Tests;

public class PartBitsTests {
    [Fact]
    public void Each_part_is_one_bit_in_order() {
        var bits = PartBits.Pack([true, false, true]);
        Assert.Equal(new byte[] { 0b101 }, bits);
        Assert.True(PartBits.IsOn(bits, 0));
        Assert.False(PartBits.IsOn(bits, 1));
        Assert.True(PartBits.IsOn(bits, 2));
    }

    [Fact]
    public void Parts_past_eight_take_another_byte() {
        // a Skull King boulder: its sprite choices, effects and damagers are children, past eight parts
        var on = new bool[9];
        on[8] = true;
        var bits = PartBits.Pack(on);
        Assert.Equal(2, bits.Length);
        Assert.True(PartBits.IsOn(bits, 8));
        Assert.False(PartBits.IsOn(bits, 7));
        Assert.False(PartBits.IsOn(bits, 9));
    }

    [Fact]
    public void Parts_past_the_most_an_update_carries_are_left_out() {
        var on = new bool[PartBits.Max + 1];
        System.Array.Fill(on, true);
        var bits = PartBits.Pack(on);
        Assert.Equal(PartBits.Max / 8, bits.Length);
        Assert.False(PartBits.Carried(PartBits.Max));
        Assert.True(PartBits.Carried(PartBits.Max - 1));
        Assert.False(PartBits.IsOn(bits, PartBits.Max));
    }
}
