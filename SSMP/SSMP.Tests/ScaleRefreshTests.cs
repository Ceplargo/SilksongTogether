using SSMP.Game.Client.Entity;
using Xunit;

namespace SSMP.Tests;

public class ScaleRefreshTests {
    [Fact]
    public void A_lost_turn_is_sent_again_even_when_the_native_stays_still() {
        Assert.False(ScaleRefresh.Due(1.05f, 1f));
        Assert.True(ScaleRefresh.Due(1.11f, 1f));
    }

    [Fact]
    public void Consecutive_lost_refreshes_do_not_stop_recovery() {
        Assert.True(ScaleRefresh.Due(1.22f, 1.11f));
        Assert.True(ScaleRefresh.Due(1.33f, 1.22f));
    }

    [Fact]
    public void A_long_frame_sends_current_scale_once_on_resuming() {
        Assert.True(ScaleRefresh.Due(8f, 1f));
        Assert.False(ScaleRefresh.Due(8f, 8f));
    }

    [Fact]
    public void An_unchanged_scale_is_not_refreshed_each_frame() {
        Assert.False(ScaleRefresh.Due(1.01f, 1f));
        Assert.False(ScaleRefresh.Due(1.02f, 1f));
    }
}
