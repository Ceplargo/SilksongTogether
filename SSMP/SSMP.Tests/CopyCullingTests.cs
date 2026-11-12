using SSMP.Game.Client.Entity.Component;
using Xunit;

// 0.3.21 (the project's 18:14 ruling 1): a thrown copy's culled Animator always animates while it is a copy, and
// gets its own mode back after, so the friend's own later use of the same pooled object culls as the game alone does
public class CopyCullingTests {
    private sealed class FakeAnimator;

    [Fact]
    public void A_copys_culled_animator_is_held_and_given_its_mode_back() {
        var culling = new CopyCulling<FakeAnimator, string>();
        var blast = new FakeAnimator();
        Assert.True(culling.Hold(blast, "CullCompletely"));
        Assert.True(culling.Release(blast, out var mode));
        Assert.Equal("CullCompletely", mode);
    }

    [Fact]
    public void The_pool_handing_it_out_again_as_a_copy_keeps_its_first_mode() {
        var culling = new CopyCulling<FakeAnimator, string>();
        var blast = new FakeAnimator();
        culling.Hold(blast, "CullCompletely");
        Assert.False(culling.Hold(blast, "AlwaysAnimate")); // a second copy before the first gave it back
        Assert.True(culling.Release(blast, out var mode));
        Assert.Equal("CullCompletely", mode);
    }

    [Fact]
    public void The_friends_own_later_use_is_not_held() {
        var culling = new CopyCulling<FakeAnimator, string>();
        var blast = new FakeAnimator();
        culling.Hold(blast, "CullCompletely");
        culling.Release(blast, out _);
        Assert.False(culling.Release(blast, out _)); // given back once: its own use keeps whatever it then has
        Assert.True(culling.Hold(blast, "CullUpdateTransforms")); // a later copy records the mode it has then
        Assert.True(culling.Release(blast, out var mode));
        Assert.Equal("CullUpdateTransforms", mode);
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)] // flame_explosion's camera shake: culled off screen in the game alone
    [InlineData(false, false, false)]
    public void Only_a_culled_animator_with_no_events_is_animated(bool culled, bool hasEvents, bool expected) =>
        Assert.Equal(expected, ThrownCopies.Animates(culled, hasEvents));
}
