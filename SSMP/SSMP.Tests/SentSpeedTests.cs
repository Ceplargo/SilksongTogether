using SSMP.Game.Client.Entity.Component;
using Xunit;

namespace SSMP.Tests;

public class SentSpeedTests {
    [Fact]
    public void A_straight_run_keeps_its_speed() {
        // a censer in flight: each send's move goes on the way the one before did
        Assert.Equal((10f, 0f), SentSpeed.Of(1f, 0f, 1f, 0f, 0.1f));
    }

    [Fact]
    public void A_curve_keeps_its_speed() {
        // a throw's arc turns a little between sends
        Assert.Equal((10f, 10f), SentSpeed.Of(1f, 0f, 1f, 1f, 0.1f));
    }

    [Fact]
    public void A_step_back_sends_no_speed() {
        // rehearsal round 1: the Last Judge's slam on her body jittering at the wall; predicted on, each step took the
        // friend's slam up to 4.3 units past the host's
        Assert.Equal((0f, 0f), SentSpeed.Of(1f, 0f, -1f, 0f, 0.1f));
        Assert.True(SentSpeed.HoldsBack(0.5f, 1f, -0.5f, -1f));
    }

    [Fact]
    public void A_step_after_a_hold_sends_no_speed() {
        // a jitter that steps, holds and steps again: the step after standing still is not a flight
        Assert.Equal((0f, 0f), SentSpeed.Of(0f, 0f, 1f, 0f, 0.1f));
    }

    [Fact]
    public void One_its_boss_carries_sends_no_speed() {
        // 2026-11-01, pair 5: the Last Judge's slam on her jittering body stepped (-0.7, 2) then (-0.5, 1) in 0.017 s;
        // sent at (-29.4, 58.8) the friend's slam flew on until the next update, up to 6.9 units off on pair 2
        Assert.Equal((0f, 0f), SentSpeed.Of(-0.7f, 2f, -0.5f, 1f, 0.017f, carried: true));
    }

    [Fact]
    public void One_its_boss_lets_go_of_keeps_its_speed() {
        // her censer in flight, thrown from her hand
        Assert.Equal((10f, 0f), SentSpeed.Of(1f, 0f, 1f, 0f, 0.1f, carried: false));
    }

    [Fact]
    public void No_time_sends_no_speed() {
        Assert.Equal((0f, 0f), SentSpeed.Of(1f, 0f, 1f, 0f, 0f));
    }
}
