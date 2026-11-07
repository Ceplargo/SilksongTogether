using SSMP.Game.Client.Entity.Component;
using Xunit;

namespace SSMP.Tests;

public class LoopStartsTests {
    [Fact]
    public void Repeated_native_plays_advance_the_shared_observation() {
        var source = new object();
        var first = LoopStarts.Watch(source);
        Assert.Equal(0u, first.Number);
        LoopStarts.Played(source);
        Assert.Same(first, LoopStarts.Watch(source));
        LoopStarts.Played(source);
        Assert.Equal(2u, first.Number);
    }

    [Fact]
    public void Unobserved_audio_does_not_create_a_past_restart() {
        var source = new object();
        LoopStarts.Played(source);
        Assert.Equal(0u, LoopStarts.Watch(source).Number);
    }

    [Fact]
    public void Each_source_has_its_own_starts() {
        var a = new object();
        var b = new object();
        var first = LoopStarts.Watch(a);
        var second = LoopStarts.Watch(b);
        LoopStarts.Played(a);
        Assert.Equal(1u, first.Number);
        Assert.Equal(0u, second.Number);
    }

    [Fact]
    public void A_sent_action_does_not_also_request_a_component_restart() {
        var source = new object();
        var starts = LoopStarts.Watch(source);
        LoopStarts.Played(source);
        LoopStarts.SentAction(source);
        Assert.Equal(0u, starts.Number);
        Assert.False(LoopStarts.NeedsPlay(true, true, 0u, starts.Number, true));
    }

    [Fact]
    public void Component_plays_survive_on_a_source_also_started_by_actions() {
        var source = new object();
        var starts = LoopStarts.Watch(source);
        LoopStarts.Played(source);
        LoopStarts.Played(source);
        LoopStarts.SentAction(source);
        Assert.Equal(1u, starts.Number);
        LoopStarts.Played(source);
        Assert.Equal(2u, starts.Number);
        Assert.True(LoopStarts.NeedsPlay(true, true, 1u, starts.Number, true));
    }

    [Fact]
    public void Reporting_one_sent_action_twice_cannot_consume_an_earlier_play() {
        var source = new object();
        var starts = LoopStarts.Watch(source);
        LoopStarts.Played(source);
        LoopStarts.Played(source);
        LoopStarts.SentAction(source);
        LoopStarts.SentAction(source);
        Assert.Equal(1u, starts.Number);
    }

    [Fact]
    public void Unobserved_or_silent_action_reports_do_not_change_other_sources() {
        var observed = new object();
        var unseen = new object();
        var starts = LoopStarts.Watch(observed);
        LoopStarts.Played(observed);
        LoopStarts.Played(unseen);
        LoopStarts.SentAction(unseen);
        Assert.Equal(1u, starts.Number);
        var later = LoopStarts.Watch(unseen);
        LoopStarts.SentAction(unseen);
        Assert.Equal(0u, later.Number);
    }

    [Theory]
    [InlineData(true, 0u, 1u, false)]
    [InlineData(true, 7u, 8u, false)]
    [InlineData(false, 0u, 1u, true)]
    public void First_playing_packet_keeps_an_action_started_loop(bool copyPlaying,
        uint previous, uint current, bool play) {
        Assert.Equal(play, LoopStarts.NeedsPlay(true, copyPlaying, previous, current, false));
    }

    [Theory]
    [InlineData(false, false, 1u, 2u, false)]
    [InlineData(false, true, 1u, 2u, false)]
    [InlineData(true, false, 0u, 0u, true)]
    [InlineData(true, true, 0u, 0u, false)]
    [InlineData(true, true, 3u, 3u, false)]
    [InlineData(true, true, 3u, 4u, true)]
    [InlineData(true, true, uint.MaxValue, 0u, true)]
    public void Only_a_new_play_restarts_an_already_playing_copy(bool playing, bool wasPlaying,
        uint previous, uint current, bool play) {
        Assert.Equal(play, LoopStarts.NeedsPlay(playing, wasPlaying, previous, current, true));
    }
}
