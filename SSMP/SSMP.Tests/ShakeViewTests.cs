using SSMP.Game.Client;
using Xunit;

namespace SSMP.Tests;

public class ShakeViewTests {
    [Theory]
    [InlineData(false, false, true, true)] // the local player's own: as in the game alone, shown or not
    [InlineData(false, true, true, true)]
    [InlineData(true, true, true, true)] // a remote hit's shake whose source shows here
    [InlineData(true, false, true, false)] // ... out of view: left out
    [InlineData(true, false, false, true)] // the switch off: as before 0.3.21
    public void A_remote_hit_shakes_this_screen_only_while_its_source_shows(bool remote, bool shows, bool on, bool shakes) {
        Assert.Equal(shakes, ShakeView.Shakes(remote, shows, on));
    }

    [Theory]
    [InlineData(true, true, false, true, true)] // the local player's tink
    [InlineData(false, true, true, true, true)] // a remote tink in view
    [InlineData(false, true, false, true, false)] // ... out of view
    [InlineData(false, true, true, false, false)] // the switch off: no remote tink shakes, as before
    [InlineData(false, false, false, false, true)] // remote attacks not kept off the local hero: as the game alone
    public void A_remote_tink_shakes_this_screen_only_while_it_shows(bool local, bool keepsOut, bool shows, bool on,
        bool shakes) {
        Assert.Equal(shakes, ShakeView.TinkShakes(local, keepsOut, shows, on));
    }
}
