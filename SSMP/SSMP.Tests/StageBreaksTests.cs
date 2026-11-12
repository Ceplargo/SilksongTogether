using SSMP.Game.Client.Entity.Component;
using Xunit;

namespace SSMP.Tests;

public class StageBreaksTests {
    // a copied Skull King boulder falling through a bone mound: the prop breaks
    [Fact]
    public void A_copys_damage_hits_a_prop() => Assert.True(StageBreaks.Keeps(true, true));

    // the same boulder over the Skull King's copy: the scene host's game hits him and sends his health
    [Fact]
    public void A_copys_damage_does_not_hit_an_enemy() => Assert.False(StageBreaks.Keeps(true, false));

    // Bone_04's Rock Roller bomb blowing up by the activated lever: the lever's tinker (a HitResponse) sparks, shakes and
    // sounds in the scene host's game, so the copy's blast hits it too (2026-11-11_085617..100408 enemy rows)
    [Fact]
    public void A_copys_damage_hits_a_hit_response() => Assert.True(StageBreaks.Keeps(true, false, true));

    [Fact]
    public void The_scene_hosts_own_damage_hits_everything() {
        Assert.True(StageBreaks.Keeps(false, false));
        Assert.True(StageBreaks.Keeps(false, true));
    }

    [Fact]
    public void Amounts_pack_a_byte_each() => Assert.Equal(new byte[] { 80, 0, 255 }, StageBreaks.Pack(new[] { 80, -1, 300 }));
}
