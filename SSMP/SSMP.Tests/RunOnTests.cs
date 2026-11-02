using SSMP.Game.Client.Entity.Component;
using Xunit;

namespace SSMP.Tests;

public class RunOnTests {
    [Fact]
    public void A_normal_frame_moves_on() {
        // a frame at 60 a second: its physics steps come within 0.02 s of the last placement
        Assert.False(RunOn.Stops(0.02f, 0f));
    }

    [Fact]
    public void A_frame_at_20_a_second_still_moves_on() {
        Assert.False(RunOn.Stops(RunOn.Limit, 0f));
    }

    [Fact]
    public void A_long_frame_stops_the_copy() {
        // the combined 0.3.15 gate: a 266 ms frame in the friend's game carried its copy of the Phantom's pin 16.8 units
        // on at the host's speed, 8.7 ahead of the host's slowing pin
        Assert.True(RunOn.Stops(0.266f, 0f));
    }

    [Fact]
    public void A_copy_placed_in_the_latest_placement_is_kept() {
        Assert.False(RunOn.Forgets(1.0f, 1.0f));
    }

    [Fact]
    public void A_copy_no_longer_placed_is_forgotten() {
        // round five's review: a copy the scene host's update left out (ended in its game) or that no update reached
        // within MaxPrediction gets no more placements, and stopped it could hang short of the wall its host's copy hit
        // (found in the code); it goes on by itself, as before the stop
        Assert.True(RunOn.Forgets(1.0f, 1.3f));
    }
}
