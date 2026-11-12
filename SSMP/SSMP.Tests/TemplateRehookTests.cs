using SSMP.Game.Client.Entity;
using Xunit;

namespace SSMP.Tests;

public class TemplateRehookTests {
    // the Pilgrim's pilgrim_behaviour: hooked as one Fsm, then rebuilt by its template's Init
    [Fact]
    public void A_rebuilt_fsm_is_hooked_again() => Assert.True(TemplateRehook.Needed(new object(), new object()));

    [Fact]
    public void The_hooked_fsm_itself_is_not() {
        var fsm = new object();
        Assert.False(TemplateRehook.Needed(fsm, fsm));
    }

    // an FSM this entity never hooked (another entity's action), or one gone
    [Fact]
    public void An_unknown_or_missing_fsm_is_not() {
        Assert.False(TemplateRehook.Needed(null, new object()));
        Assert.False(TemplateRehook.Needed(new object(), null));
    }
}
