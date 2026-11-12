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

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Damage_updates_leave_root_activation_to_the_entity(bool isRoot, bool copy) {
        Assert.Equal(copy, DamagePairs.CopiesActivation(isRoot));
    }

    [Theory]
    [InlineData(true, false, true, true)]
    [InlineData(false, false, true, false)]
    [InlineData(true, true, true, false)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    public void Only_paired_damage_children_replace_reset_on_exit_replay(
        bool resetOnExit, bool recursive, bool pairedChild, bool useDamage) {
        Assert.Equal(useDamage, DamagePairs.UsesNativeActivation(resetOnExit, recursive, pairedChild));
    }

    [Fact]
    public void A_late_old_damage_state_gets_a_current_snapshot_even_without_another_attack() {
        Assert.False(DamagePairs.RefreshDue(1.04f, 1f));
        Assert.True(DamagePairs.RefreshDue(1.06f, 1f));
    }

    [Fact]
    public void A_late_retry_gets_a_fresh_state_before_the_next_tenth_of_a_second() {
        // Recorded retry at .288 s followed a current snapshot at .242 s; waiting until .345 left it active.
        Assert.True(DamagePairs.RefreshDue(.3f, .242f));
    }

    [Fact]
    public void Losing_a_refresh_does_not_stop_damage_recovery() {
        Assert.True(DamagePairs.RefreshDue(1.22f, 1.11f));
        Assert.True(DamagePairs.RefreshDue(1.33f, 1.22f));
    }

    [Fact]
    public void A_long_frame_sends_current_damage_once_on_resuming() {
        Assert.True(DamagePairs.RefreshDue(8f, 1f));
        Assert.False(DamagePairs.RefreshDue(8f, 8f));
    }

    [Fact]
    public void Unchanged_damage_is_not_sent_every_frame() {
        Assert.False(DamagePairs.RefreshDue(1.01f, 1f));
        Assert.False(DamagePairs.RefreshDue(1.02f, 1f));
    }

    [Fact]
    public void A_box_left_without_a_match_is_named() {
        string[] clone = ["", "Hit GDash", "Dragoon Blast/hero damager", "Extra"];
        string[] original = ["", "Hit GDash", "Pin Projectile", "Dragoon Blast/hero damager"];
        Assert.Equal(["the original's Pin Projectile has none on the clone", "the clone's Extra has none on the original"],
            DamagePairs.Unmatched(original, clone, DamagePairs.Align(original, clone)));
    }

    [Fact]
    public void A_copys_sound_sources_pair_by_path_and_one_with_no_partner_stays_silent() {
        // 0.3.20's Dock Worker: his copy holds one more AudioSource than the original, so SSMP made no loop follow and
        // his footsteps were silent on the friend's screen; with fewer on the copy, each paired source still follows
        string[] original = ["", "Audio Voice", "Audio Footsteps"];
        string[] clone = ["Audio Footsteps", "", "Audio Extra"];
        Assert.Equal(["clone root", null, "clone Footsteps"],
            DamagePairs.Paired(["clone Footsteps", "clone root", "clone Extra"], DamagePairs.Align(original, clone)));
        Assert.Equal([null, null], DamagePairs.Paired(["clone root"], DamagePairs.Align(["Audio A", "Audio B"], [""])));
        Assert.False(DamagePairs.AnyPaired(DamagePairs.Align(["Audio A"], ["Audio B"])));
        Assert.True(DamagePairs.AnyPaired(DamagePairs.Align(original, clone)));
    }
}
