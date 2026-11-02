using Xunit;

namespace BossRule.Tests;

public class MultiHitPathTests {
    [Fact]
    public void A_copys_box_is_named_by_its_path_below_the_boss_without_clone() {
        // SSMP's copy of the Widow is a root object; her own sits in her boss scene
        Assert.Equal("Spinner Boss/DashSlash Hit", MultiHitPath.Of("Spinner Boss(Clone)/DashSlash Hit"));
        Assert.Equal("Phantom/Parry Swipe/phantom_parry_triple_character/Hero_Damager_Pin",
            MultiHitPath.Of("Phantom(Clone)/Parry Swipe/phantom_parry_triple_character/Hero_Damager_Pin"));
    }

    [Fact]
    public void The_scene_hosts_own_box_is_the_one_its_path_ends_with() {
        const string named = "Spinner Boss/DashSlash Hit";
        Assert.True(MultiHitPath.Names(
            "Black Thread States Thread Only Variant/Normal World/Boss Scene/Spinner Boss/DashSlash Hit", named));
        Assert.True(MultiHitPath.Names("Spinner Boss/DashSlash Hit", named));
        // not another boss whose name only ends the same, nor her copy
        Assert.False(MultiHitPath.Names("Boss Scene/Big Spinner Boss/DashSlash Hit", named));
        Assert.False(MultiHitPath.Names("Spinner Boss(Clone)/DashSlash Hit", named));
    }

    [Fact]
    public void Only_a_copys_box_is_told() {
        // a room's own multi-hitter (a steam vent's) wounds a hero in every game alike
        Assert.True(MultiHitPath.IsCopy("Vampire Gnat(Clone)/Slash Collider"));
        Assert.False(MultiHitPath.IsCopy("Steam Vent/Damager"));
        Assert.False(MultiHitPath.IsCopy("Slash Collider"));
    }

    [Fact]
    public void A_message_reads_back_as_the_player_and_the_box() {
        Assert.Equal(("Friend|1", "Vampire Gnat/Slash Collider"),
            MultiHitPath.Read(MultiHitPath.Message("Friend|1", "Vampire Gnat/Slash Collider")));
        Assert.Null(MultiHitPath.Read("no bar"));
    }
}
