using Xunit;

namespace BossRule.Tests;

// probe_shakra_fight (0.3.22 save checks Task 10): a Bone Goomba moved onto Shakra set off her Attack Enemies in each
// game on its own, and the two fights went apart (the friend's threw once more and landed 0.6 s later)
public class FightRuleTests {
    [Fact]
    public void Only_the_scene_hosts_game_sends_her_fight() {
        Assert.True(FightRule.Sends(true, "Attack Enemies"));
        Assert.False(FightRule.Sends(false, "Attack Enemies"));
        Assert.False(FightRule.Sends(true, "Dialogue"));
    }

    [Fact]
    public void A_connected_game_that_does_not_run_the_room_follows_hers() {
        Assert.True(FightRule.Follows(true, false));
        Assert.False(FightRule.Follows(true, true));
        Assert.False(FightRule.Follows(false, false));
    }

    // her Idle's Enemy Range trigger sets off a fight: a following game leaves it out, so no fight of its own starts
    [Fact]
    public void A_following_game_starts_no_fight_of_its_own() {
        Assert.True(FightRule.SkipsOwn(true, "Attack Enemies", "Idle"));
        Assert.False(FightRule.SkipsOwn(false, "Attack Enemies", "Idle"));
        Assert.False(FightRule.SkipsOwn(true, "Attack Enemies", "Throw"));
        Assert.False(FightRule.SkipsOwn(true, "Dialogue", "Idle"));
    }

    [Fact]
    public void Her_sounds_flash_and_song_events_play() {
        Assert.Equal(FightRule.Play.Sound, FightRule.Of("PlayAudioEvent", true, null, null));
        Assert.Equal(FightRule.Play.Sound, FightRule.Of("PlayAudioEventRandom", true, null, null));
        Assert.Equal(FightRule.Play.Sound, FightRule.Of("AudioPlayRandomVoiceFromTableV2", false, "Audio Loop Voice", null));
        Assert.Equal(FightRule.Play.Activate, FightRule.Of("ActivateGameObject", false, "Ring Flash", null));
        Assert.Equal(FightRule.Play.Event, FightRule.Of("SendEventByName", false, null, "END ANIM"));
        Assert.Equal(FightRule.Play.Event, FightRule.Of("SendEventByName", false, null, "MAPPER READY"));
    }

    // the project's ruling, 2026-11-12: her thrown disc shows in every game (The Marrow at 100%), its hits left
    // out in a following game (the enemies' health is the scene host's)
    [Fact]
    public void Her_thrown_disc_is_thrown_again() {
        Assert.Equal(FightRule.Play.Throw, FightRule.Of("FlingObjectsFromGlobalPool", false, null, null));
    }

    // shakra_fight 171222: her Battle Cry Start sets her Dialogue's Attacking, which stops her startle; a following game
    // left it unset and her own Dialogue startled in the copied fight
    [Fact]
    public void Her_fights_flags_on_her_own_fsms_are_set() {
        Assert.Equal(FightRule.Play.Event, FightRule.Of("SetFsmBool", true, null, null));
        Assert.Equal(FightRule.Play.None, FightRule.Of("SetFsmBool", false, "Hero_Hornet(Clone)", null));
    }

    // shakra_discs' first green, 170553 and 170803: thrown alike, the scene host's disc turned back at her target enemy
    // and the other game's flew on, so each disc's flight goes as frames of its own, by the throw's number
    [Fact]
    public void A_discs_frames_go_by_its_number() {
        Assert.Equal("@disc:3/7", FightRule.DiscTag(3, 7));
        Assert.Equal((3, 7), FightRule.DiscOf("@disc:3/7"));
        Assert.Null(FightRule.DiscOf("@disc:3"));
        Assert.Null(FightRule.DiscOf("Throw#3"));
        Assert.Null(FightRule.DiscOf("Idle"));
        Assert.Null(FightRule.FromTag("@disc:3/7"));
    }

    // shakra_discs --lag 50, 173350: with 1% loss, a resent older frame came after a newer one and the copy held a stale
    // place 3.2 units off once her disc stopped; an older frame after "went" would throw the disc again
    [Fact]
    public void A_discs_older_frame_is_left_out() {
        Assert.True(FightRule.Newer(0, null));
        Assert.True(FightRule.Newer(8, 7));
        Assert.False(FightRule.Newer(7, 7));
        Assert.False(FightRule.Newer(6, 7));
    }

    // her moves (sent as frames), a talk's end, the camera and the hero
    [Fact]
    public void Nothing_else_plays() {
        Assert.Equal(FightRule.Play.None, FightRule.Of("SpawnObjectFromGlobalPool", false, null, null));
        Assert.Equal(FightRule.Play.None, FightRule.Of("Translate", true, null, null));
        Assert.Equal(FightRule.Play.None, FightRule.Of("Tk2dPlayAnimationWithEvents", true, null, null));
        Assert.Equal(FightRule.Play.None, FightRule.Of("SendEventByName", false, null, "END CONVO"));
        Assert.Equal(FightRule.Play.None, FightRule.Of("DoCameraShake", false, null, null));
        Assert.Equal(FightRule.Play.None, FightRule.Of("PlayAudioEvent", false, "Hero_Hornet(Clone)", null));
        Assert.Equal(FightRule.Play.None, FightRule.Of("ActivateGameObject", false, "Body Collider", null));
    }

    // shakra_fight's first green: her Arriving Voice? tests a flag before its voice line, so the scene host's game
    // played none there and a game replaying the state's actions played one at every Tele In. Each played action now
    // goes as that game ran it, named by its state and place in the state
    [Fact]
    public void A_played_action_is_named_by_its_state_and_place() {
        Assert.Equal("Arriving Voice?#1", FightRule.ActionTag("Arriving Voice?", 1));
        Assert.Equal(("Tele In", 2), FightRule.FromTag("Tele In#2"));
        Assert.Null(FightRule.FromTag("Tele In"));
        Assert.Null(FightRule.FromTag("Idle"));
    }

    [Fact]
    public void A_frame_goes_when_her_look_changes_or_she_moved_after_a_short_wait() {
        Assert.True(FightRule.SendsFrame(true, false, 0f));
        Assert.True(FightRule.SendsFrame(false, true, 0.06f));
        Assert.False(FightRule.SendsFrame(false, true, 0.01f));
        Assert.False(FightRule.SendsFrame(false, false, 1f));
    }
}
