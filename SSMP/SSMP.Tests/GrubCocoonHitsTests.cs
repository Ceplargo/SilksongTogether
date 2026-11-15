using SSMP.Game.Client;
using Xunit;

namespace SSMP.Tests;

public class GrubCocoonHitsTests {
    // Dock_14 (probe_grub_drop): the host's swings broke the cocoon in the friend's game too, which gave the friend's
    // hero silk and dropped a second Silk Grub; the hitter's own game keeps both
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void A_remote_players_hit_keeps_the_drop_and_silk_the_hitters(bool remote, bool keepsOff) =>
        Assert.Equal(keepsOff, GrubCocoonHits.KeepsOwn(remote));
}
