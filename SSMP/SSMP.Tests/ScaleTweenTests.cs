using SSMP.Game.Client.Entity.Action;
using Xunit;

namespace SSMP.Tests;

public class ScaleTweenTests {
    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(.03125f, .38268343f)]
    [InlineData(.0625f, .70710678f)]
    [InlineData(.125f, 1f)]
    [InlineData(.5f, 1f)]
    public void Spider_growth_uses_the_native_sine_curve(float elapsed, float expected) =>
        Assert.Equal(expected, ScaleTween.Progress(elapsed, 0f, .125f, 2), 5);

    [Theory]
    [InlineData(0, .5f)]
    [InlineData(1, .75f)]
    [InlineData(2, .70710678f)]
    [InlineData(99, .5f)]
    public void Other_curves_and_unknown_curve_match_the_native_fallback(int curve, float expected) =>
        Assert.Equal(expected, ScaleTween.Progress(.7f, .2f, 1f, curve), 5);

    [Fact]
    public void Delay_holds_the_start_scale() =>
        Assert.Equal(0f, ScaleTween.Progress(.1f, .2f, 1f, 2));

    [Fact]
    public void Zero_duration_reaches_target_after_its_delay() {
        Assert.Equal(0f, ScaleTween.Progress(.1f, .2f, 0f, 2));
        Assert.Equal(1f, ScaleTween.Progress(.3f, .2f, 0f, 2));
    }
}
