using SSMP.Game.Server.Save;
using Xunit;

namespace SSMP.Tests;

public class AppliedSaveNumbersTests {
    [Fact]
    public void A_new_number_is_applied() => Assert.True(new AppliedSaveNumbers().TryAdd(5));

    [Fact]
    public void A_number_seen_before_is_not_applied_again() {
        var applied = new AppliedSaveNumbers();
        applied.TryAdd(5);
        Assert.False(applied.TryAdd(5));
    }

    [Fact]
    public void Numbers_out_of_order_each_apply_once() {
        var applied = new AppliedSaveNumbers();
        Assert.True(applied.TryAdd(10));
        Assert.True(applied.TryAdd(8));
        Assert.True(applied.TryAdd(9));
        Assert.False(applied.TryAdd(8));
        Assert.False(applied.TryAdd(10));
    }

    [Fact]
    public void Only_the_last_4096_are_kept() {
        var applied = new AppliedSaveNumbers();
        applied.TryAdd(1);
        for (ulong n = 2; n <= 5001; n++) {
            applied.TryAdd(n);
        }

        // Number 1 is no longer kept: older than every kept number, it counts as applied long ago
        Assert.False(applied.TryAdd(1));
        Assert.True(applied.TryAdd(5002));
    }
}
