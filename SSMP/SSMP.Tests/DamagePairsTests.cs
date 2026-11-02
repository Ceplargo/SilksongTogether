using SSMP.Game.Client.Entity;
using Xunit;

namespace SSMP.Tests;

public class DamagePairsTests {
    private static readonly string[] PhantomOriginal = [
        "", "Hit GDash", "Hit ADash", "Hit Stab 1", "Hit Stab 2", "Pin Projectile", "Dragoon Blast/hero damager",
        "Dragoon Down", "Dragoon Down/Dragoon Down Damager", "Parry Swipe/phantom_parry_triple_character/Hero_Damager_Pin",
        "Catch Hit Stab"
    ];

    [Fact]
    public void The_clones_hurt_boxes_pair_by_path_past_one_it_lacks() {
        // round five: the Phantom's clone has no Pin Projectile (her FSM let go of it), so by index every box after it
        // took the next one's damage and on/off: her blast the pin's (1, not 2), her Dragoon Down the blast's
        string[] clone = [
            "", "Hit GDash", "Hit ADash", "Hit Stab 1", "Hit Stab 2", "Dragoon Blast/hero damager", "Dragoon Down",
            "Dragoon Down/Dragoon Down Damager", "Parry Swipe/phantom_parry_triple_character/Hero_Damager_Pin",
            "Catch Hit Stab"
        ];
        Assert.Equal([0, 1, 2, 3, 4, -1, 5, 6, 7, 8, 9], DamagePairs.Align(PhantomOriginal, clone));
    }

    [Fact]
    public void A_clone_with_far_fewer_hurt_boxes_pairs_each_of_its_own() {
        // the Last Judge: 22 on her original, 5 on her clone (no swing, throw or stomp boxes); by index her clone's
        // Roar Glow damager took Swing Attack/Hit3's damage and on/off
        string[] original = [
            "", "Swing Attack/Hit1", "Swing Attack/Hit2", "Swing Attack/Hit3", "Censer Slam", "Censer Throw",
            "Censer Slam Flame/censer sphere/hero_damager", "Roar Glow/Damager", "Stomp Flame/Damager"
        ];
        string[] clone = ["", "Censer Slam", "Censer Slam Flame/censer sphere/hero_damager", "Roar Glow/Damager"];
        Assert.Equal([0, -1, -1, -1, 1, -1, 2, 3, -1], DamagePairs.Align(original, clone));
    }

    [Fact]
    public void Same_named_siblings_pair_in_their_order_among_them() {
        // two hurt boxes of one path (same-named siblings): the first with the first, the second with the second
        Assert.Equal([1, 0, 2], DamagePairs.Align(["Hit", "Body", "Hit"], ["Body", "Hit", "Hit"]));
        Assert.Equal([1, 0, -1], DamagePairs.Align(["Hit", "Body", "Hit"], ["Body", "Hit"]));
    }

    [Fact]
    public void A_box_left_without_a_match_is_named() {
        string[] clone = ["", "Hit GDash", "Dragoon Blast/hero damager", "Extra"];
        string[] original = ["", "Hit GDash", "Pin Projectile", "Dragoon Blast/hero damager"];
        Assert.Equal(["the original's Pin Projectile has none on the clone", "the clone's Extra has none on the original"],
            DamagePairs.Unmatched(original, clone, DamagePairs.Align(original, clone)));
    }
}
