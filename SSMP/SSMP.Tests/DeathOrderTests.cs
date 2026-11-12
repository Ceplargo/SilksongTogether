using SSMP.Game.Client.Entity;
using Xunit;

namespace SSMP.Tests;

public class DeathOrderTests {
    [Theory]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, false)]
    public void Only_a_dying_copy_defers_its_hide(bool active, bool dying, bool defer) {
        Assert.Equal(defer, DeathOrder.FinishesDeathBeforeHide(active, dying));
    }

    // index, the last hurt-box data before the death, the death (-1: none), the frame a box came on, this frame
    [Theory]
    [InlineData(1, 0, 2, 7, 7, true)] // the box's data went in; the rest, from the killing hit to the death, waits
    [InlineData(2, 0, 2, 7, 7, true)]
    [InlineData(0, 0, 2, 7, 7, false)] // the box's own data is applied
    [InlineData(0, -1, 1, 7, 7, true)] // a box came on by an earlier update this frame: all of this one waits
    [InlineData(3, 0, 2, 7, 7, false)] // after the death
    [InlineData(1, 0, 2, 6, 7, false)] // the box came on a frame earlier: it showed
    [InlineData(1, 0, 2, 8, 7, false)]
    [InlineData(0, -1, -1, 7, 7, false)] // no death
    public void A_copy_death_waits_only_for_a_box_that_came_on_in_its_frame(int index, int lastBox, int death,
        int boxOnFrame, int frame, bool waits) {
        Assert.Equal(waits, DeathOrder.WaitsForBox(index, lastBox, death, boxOnFrame, frame));
    }
}
