using SSMP.Game.Client;
using Xunit;

namespace SSMP.Tests;

public class HeroStepsTests {
    [Fact]
    public void Starting_to_run_with_no_steps_going_plays_the_run_start() =>
        Assert.True(HeroSteps.RunStarted(footstepSound: true, startedBefore: false, startedAfter: true, transitioning: false));

    [Fact]
    public void Running_on_plays_no_second_run_start() =>
        Assert.False(HeroSteps.RunStarted(footstepSound: true, startedBefore: true, startedAfter: true, transitioning: false));

    [Fact]
    public void A_start_in_a_room_change_plays_no_run_start() =>
        Assert.False(HeroSteps.RunStarted(footstepSound: true, startedBefore: false, startedAfter: true, transitioning: true));

    [Fact]
    public void A_start_with_footsteps_already_going_plays_no_run_start() =>
        Assert.False(HeroSteps.RunStarted(footstepSound: true, startedBefore: false, startedAfter: false, transitioning: false));

    [Fact]
    public void Another_sound_plays_no_run_start() =>
        Assert.False(HeroSteps.RunStarted(footstepSound: false, startedBefore: false, startedAfter: true, transitioning: false));
}
