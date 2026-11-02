using SSMP.Game.Client.Entity.Component;
using Xunit;

namespace SSMP.Tests;

public class UpdateOrderTests {
    [Fact]
    public void A_newer_update_is_applied() {
        Assert.False(UpdateOrder.IsStale(101, 100));
        Assert.False(UpdateOrder.IsStale(7, -1)); // none applied yet
    }

    [Fact]
    public void The_same_or_an_older_update_is_a_resend() {
        Assert.True(UpdateOrder.IsStale(100, 100));
        Assert.True(UpdateOrder.IsStale(90, 100));
    }

    [Fact]
    public void Numbers_wrap() {
        Assert.True(UpdateOrder.IsStale(65535, 5));
        Assert.False(UpdateOrder.IsStale(3, 65535));
    }

    [Fact]
    public void A_number_far_back_is_another_senders() {
        Assert.False(UpdateOrder.IsStale(100 - UpdateOrder.Window, 100));
        Assert.False(UpdateOrder.IsStale(1000, 30000));
    }
}
