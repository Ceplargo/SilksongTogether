using SSMP.Game.Client;
using Xunit;

namespace SSMP.Tests;

public class SlashRepeatRuleTests {
    // Bone_16, the host's game: SlashAlt started again 17 ms later, for the same attack
    [Fact]
    public void The_same_clip_for_the_same_attack_is_a_repeat() {
        Assert.True(SlashRepeatRule.IsRepeat("SlashAlt", 7, 100.000, "SlashAlt", 7, 100.017));
    }

    [Fact]
    public void A_new_attack_is_not_however_soon() {
        Assert.False(SlashRepeatRule.IsRepeat("Slash", 7, 100.000, "Slash", 8, 100.001));
    }

    [Theory]
    [InlineData("SlashAlt", "Slash")]           // another clip
    [InlineData("Dash Attack", "Dash Attack")]  // not a clip Attack() plays: crest moves run from their own FSMs
    public void Another_clip_is_not(string lastClip, string clip) {
        Assert.False(SlashRepeatRule.IsRepeat(lastClip, 7, 100.000, clip, 7, 100.017));
    }

    [Theory]
    [InlineData(100.6)]   // past RepeatS: an attack count put back to an old value
    [InlineData(99.9)]    // a clock that went back
    public void Out_of_time_is_not(double now) {
        Assert.False(SlashRepeatRule.IsRepeat("SlashAlt", 7, 100.0, "SlashAlt", 7, now));
    }

    [Fact]
    public void The_first_clip_is_not() {
        Assert.False(SlashRepeatRule.IsRepeat(null, 0, 0, "Slash", 0, 100.0));
    }
}
