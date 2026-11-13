using SSMP.Game.Client;
using Xunit;

namespace SSMP.Tests;

public class DustSpotsTests {
    [Fact]
    public void A_platforms_place_goes_and_comes_back() =>
        Assert.Equal((12.5f, -3.25f), DustSpots.Read(DustSpots.Info(12.5f, -3.25f)));

    [Fact]
    public void A_wrong_info_is_none() {
        Assert.Null(DustSpots.Read(null));
        Assert.Null(DustSpots.Read(new byte[4]));
    }

    // Bone_08's dusty platforms sit apart; the sent place is the landing game's own platform
    [Fact]
    public void The_platform_nearest_the_sent_place_is_found() {
        var spots = new[] { (0f, 0f), (10f, 5f), (10.2f, 5f) };
        Assert.Equal(1, DustSpots.Nearest(spots, 10f, 5f));
        Assert.Equal(2, DustSpots.Nearest(spots, 10.15f, 5f));
    }

    // probe_plat_dust: Bone_06's Bone Circler (1) landed on two dusty platforms in the host's game, no dirt in the
    // friend's; a corpse, geo piece or debris falls by each game's own physics
    [Fact]
    public void A_heros_or_a_live_enemys_landing_is_sent() {
        Assert.True(DustSpots.Sends(true, false));
        Assert.True(DustSpots.Sends(false, true));
        Assert.False(DustSpots.Sends(false, false));
    }

    [Fact]
    public void No_platform_near_the_sent_place_is_none() {
        Assert.Equal(-1, DustSpots.Nearest(new[] { (0f, 0f) }, 3f, 0f));
        Assert.Equal(-1, DustSpots.Nearest(System.Array.Empty<(float, float)>(), 0f, 0f));
    }
}
