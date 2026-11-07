using SSMP.Game.Client.Entity.Component;
using Xunit;

public class ThrownCopiesTests {
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, true, false)]
    public void A_live_native_throw_gets_one_copy_even_after_the_first_update(bool first, bool hasCopy, bool expected) =>
        Assert.Equal(expected, ThrownCopies.NeedsCopy(first, hasCopy, false));

    // The whole-branch review (2026-11-05): a copy that ended itself here (it broke on the friend, or landed first) was
    // made again from the next update while the host's still flew; only one its parent took away is made again.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_copy_that_ended_itself_is_not_made_again(bool first) =>
        Assert.False(ThrownCopies.NeedsCopy(first, false, true));
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Only_a_recovered_throw_is_stopped_by_its_native_end(bool recovered, bool expected) =>
        Assert.Equal(expected, ThrownCopies.StopsAtNativeEnd(recovered));
}
