using SSMP.Game.Client.Entity;
using Xunit;

// probe_dung (0.3.21): Bone_01's Rock Roller Dung was out in the scene host's game only, an enemy copy in the other's
public class ScentPilesTests {
    [Theory]
    [InlineData("Rock Roller Dung")]
    [InlineData("Rock Roller RockDebris")]
    [InlineData("Rock Roller RockDebris (1)")]
    public void A_wishs_scent_pile_is_not_an_enemy(string name) => Assert.True(ScentPiles.IsPile(name));

    [Theory]
    [InlineData("Rock Roller")]
    [InlineData("Rock Roller (2)")]
    [InlineData("Rock Roller Scene")]
    public void The_rock_roller_itself_stays_one(string name) => Assert.False(ScentPiles.IsPile(name));
}
