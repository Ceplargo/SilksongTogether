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
}
