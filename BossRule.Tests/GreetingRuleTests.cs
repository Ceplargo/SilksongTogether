using Xunit;

namespace BossRule.Tests;

// probe_shakra (0.3.22 save checks Task 10): in Bone_04 Shakra's ring shout, its sounds and her meeting talk played in the
// walker's game only
public class GreetingRuleTests {
    [Fact]
    public void Only_her_shout_dialogue_and_singing_are_followed() {
        Assert.True(GreetingRule.Follows("Mapper NPC", "Ring Shout"));
        Assert.True(GreetingRule.Follows("Mapper NPC", "Dialogue"));
        Assert.True(GreetingRule.Follows("Mapper NPC", "Rest Animation"));
        Assert.False(GreetingRule.Follows("Mapper NPC", "Attack Enemies"));
        Assert.False(GreetingRule.Follows("Sherma NPC", "Dialogue"));
    }

    [Fact]
    public void The_greeting_runs_from_taking_the_hero_until_she_sings_again() {
        Assert.True(GreetingRule.Starts("Ring Shout", "Take Control"));
        Assert.False(GreetingRule.Starts("Ring Shout", "Ready"));
        Assert.True(GreetingRule.Ends("Rest Animation", "Turn Right"));
        Assert.True(GreetingRule.Ends("Rest Animation", "Start Singing"));
        Assert.False(GreetingRule.Ends("Rest Animation", "Sing Left"));
        Assert.False(GreetingRule.Ends("Dialogue", "Stand Up"));
    }

    [Fact]
    public void The_walker_sends_its_greetings_states_and_its_end_only() {
        Assert.True(GreetingRule.Sends(true, "Ring Shout", "Shout Burst"));
        Assert.True(GreetingRule.Sends(true, "Dialogue", "Meet2"));
        Assert.True(GreetingRule.Sends(true, "Rest Animation", "Turn Right"));
        Assert.False(GreetingRule.Sends(true, "Rest Animation", "Idle"));
        Assert.False(GreetingRule.Sends(false, "Ring Shout", "Shout Burst"));
        Assert.False(GreetingRule.Sends(false, "Dialogue", "Idle"));
    }

    [Fact]
    public void A_game_whose_own_greeting_runs_plays_no_copy() {
        Assert.True(GreetingRule.Plays(false));
        Assert.False(GreetingRule.Plays(true));
    }

    [Fact]
    public void Her_own_animation_sounds_flash_and_song_stop_play() {
        Assert.Equal(GreetingRule.Play.Clip, GreetingRule.Of("Tk2dPlayAnimationWithEvents", true, null, null));
        Assert.Equal(GreetingRule.Play.Clip, GreetingRule.Of("Tk2dPlayAnimation", true, null, null));
        Assert.Equal(GreetingRule.Play.Sound, GreetingRule.Of("PlayAudioEvent", true, null, null));
        Assert.Equal(GreetingRule.Play.Sound, GreetingRule.Of("PlayRandomAudioClipTable", true, null, null));
        Assert.Equal(GreetingRule.Play.Activate, GreetingRule.Of("ActivateGameObject", false, "Ring Flash", null));
        Assert.Equal(GreetingRule.Play.Event, GreetingRule.Of("SendEventByName", false, null, "END ANIM"));
    }

    // Hero Anim's hero actions, the camera lock, the dialogue, the shop and the save stay the walker's
    [Fact]
    public void Nothing_that_holds_the_hero_or_opens_talk_plays() {
        Assert.Equal(GreetingRule.Play.None, GreetingRule.Of("Tk2dPlayAnimation", false, null, null));
        Assert.Equal(GreetingRule.Play.None, GreetingRule.Of("SetVelocity2d", true, null, null));
        Assert.Equal(GreetingRule.Play.None, GreetingRule.Of("AudioPlayerOneShotSingle", false, null, null));
        Assert.Equal(GreetingRule.Play.None, GreetingRule.Of("ActivateGameObject", false, "CamLock FirstMeet", null));
        Assert.Equal(GreetingRule.Play.None, GreetingRule.Of("SendEventByName", false, null, "DO MEET"));
        Assert.Equal(GreetingRule.Play.None, GreetingRule.Of("RunDialogueV4", true, null, null));
        Assert.Equal(GreetingRule.Play.None, GreetingRule.Of("EndDialogue", true, null, null));
        Assert.Equal(GreetingRule.Play.None, GreetingRule.Of("SetPlayerDataBool", false, null, null));
        Assert.Equal(GreetingRule.Play.None, GreetingRule.Of("StartRoarEmitter", true, null, null));
        Assert.Equal(GreetingRule.Play.None, GreetingRule.Of("RunFSM", false, null, null));
    }
}
