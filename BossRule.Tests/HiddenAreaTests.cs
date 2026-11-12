using BossRule;
using Xunit;

namespace BossRule.Tests;

public class HiddenAreaTests {
    // Bone_01's Inverse Remaskers (world sweep, 0.3.20): the doer's screen showed the area open, the other's covered
    [Fact]
    public void A_friend_inside_opens_the_area_here() => Assert.True(HiddenArea.OpenFor(false, true));

    [Fact]
    public void With_nobody_else_inside_it_is_the_games_own() => Assert.False(HiddenArea.OpenFor(false, false));

    // Bone_08's Remasker New Sharp Ultra, a blackout mask: the friend's screen kept a black cover over the cave the host
    // stood in (probe_blackout08, 2026-11-11); the screen-wide dark stays the inside hero's (only its own Entered adds it)
    [Fact]
    public void A_blackout_opens_for_a_friend_inside_too() => Assert.True(HiddenArea.OpenFor(true, true));

    // its cover, open (blue), still drew the cave black on the screen of a hero outside it (probe_cave08_hide, 2026-11-11)
    [Fact]
    public void A_blackout_open_for_a_friend_hides_its_cover_from_a_hero_outside() =>
        Assert.True(HiddenArea.HidesCover(true, true, false));

    [Fact]
    public void A_hero_inside_keeps_the_cover_its_own_dark_needs() => Assert.False(HiddenArea.HidesCover(true, true, true));

    [Fact]
    public void A_closed_blackout_keeps_its_cover() => Assert.False(HiddenArea.HidesCover(true, false, false));

    [Fact]
    public void A_plain_mask_has_no_blackout_cover() => Assert.False(HiddenArea.HidesCover(false, true, false));
}
