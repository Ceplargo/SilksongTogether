using SSMP.Game.Client.Entity.Component;
using Xunit;

namespace SSMP.Tests;

public class LoopStartsTests {
    [Fact]
    public void Repeated_native_plays_advance_the_shared_observation() {
        var source = new object();
        var first = LoopStarts.Watch(source);
        Assert.Equal(0u, first.Number);
        LoopStarts.Played(source, 0f);
        Assert.Same(first, LoopStarts.Watch(source));
        LoopStarts.Played(source, 0f);
        Assert.Equal(2u, first.Number);
    }

    [Fact]
    public void Unobserved_audio_does_not_create_a_past_restart() {
        var source = new object();
        LoopStarts.Played(source, 0f);
        Assert.Equal(0u, LoopStarts.Watch(source).Number);
    }

    [Fact]
    public void Each_source_has_its_own_starts() {
        var a = new object();
        var b = new object();
        var first = LoopStarts.Watch(a);
        var second = LoopStarts.Watch(b);
        LoopStarts.Played(a, 0f);
        Assert.Equal(1u, first.Number);
        Assert.Equal(0u, second.Number);
    }

    [Fact]
    public void A_sent_action_does_not_also_request_a_component_restart() {
        var source = new object();
        var starts = LoopStarts.Watch(source);
        LoopStarts.Played(source, 0f);
        LoopStarts.SentAction(source);
        Assert.Equal(0u, starts.Number);
        Assert.False(LoopStarts.NeedsPlay(true, true, 0u, starts.Number, true));
    }

    [Fact]
    public void Component_plays_survive_on_a_source_also_started_by_actions() {
        var source = new object();
        var starts = LoopStarts.Watch(source);
        LoopStarts.Played(source, 0f);
        LoopStarts.Played(source, 0f);
        LoopStarts.SentAction(source);
        Assert.Equal(1u, starts.Number);
        LoopStarts.Played(source, 0f);
        Assert.Equal(2u, starts.Number);
        Assert.True(LoopStarts.NeedsPlay(true, true, 1u, starts.Number, true));
    }

    [Fact]
    public void Reporting_one_sent_action_twice_cannot_consume_an_earlier_play() {
        var source = new object();
        var starts = LoopStarts.Watch(source);
        LoopStarts.Played(source, 0f);
        LoopStarts.Played(source, 0f);
        LoopStarts.SentAction(source);
        LoopStarts.SentAction(source);
        Assert.Equal(1u, starts.Number);
    }

    [Fact]
    public void Unobserved_or_silent_action_reports_do_not_change_other_sources() {
        var observed = new object();
        var unseen = new object();
        var starts = LoopStarts.Watch(observed);
        LoopStarts.Played(observed, 0f);
        LoopStarts.Played(unseen, 0f);
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

    [Fact]
    public void A_one_shot_on_a_stopped_copy_loop_lets_the_host_start_it() {
        // Bone_18's Pilgrim 05: his land one-shot still sounded on the walk loop's source as the host's walk came
        var looping = LoopStarts.CopyLoops(isPlaying: true, moves: false, wasReportedPlaying: false);
        Assert.False(looping);
        Assert.True(LoopStarts.NeedsPlay(true, looping, 3, 4, false));
    }

    [Theory]
    [InlineData(true, true, false, true)]
    [InlineData(true, false, true, true)]
    [InlineData(false, true, true, false)]
    public void A_moving_or_reported_copy_loop_counts_as_playing(bool isPlaying, bool moves, bool reported,
        bool looping) {
        Assert.Equal(looping, LoopStarts.CopyLoops(isPlaying, moves, reported));
    }

    [Theory]
    [InlineData(10f, 10f, true)]
    [InlineData(10f, 10.017f, true)]
    [InlineData(10f, 10.3f, false)]
    [InlineData(float.NegativeInfinity, 0f, false)]
    public void A_copy_loop_counts_as_moving_only_while_it_starts(float playedAt, float now, bool started) {
        // a copied FSM played the beastflies' charge loop; the scene host's start of it came a frame later
        Assert.Equal(started, LoopStarts.JustStarted(playedAt, now));
    }

    [Fact]
    public void A_loop_play_keeps_its_time_until_the_follow_stops_the_source() {
        var source = new object();
        var starts = LoopStarts.Watch(source);
        Assert.Equal(float.NegativeInfinity, starts.PlayedAt);
        LoopStarts.Played(source, 5f);
        Assert.Equal(5f, starts.PlayedAt);
        starts.Stopped();
        Assert.Equal(float.NegativeInfinity, starts.PlayedAt);
    }

    [Fact]
    public void A_copy_source_the_clone_lacks_has_no_starts() {
        // Dock_09's Dock Guard Slasher (the 0.3.21 gate, 2026-11-11_123256): its clone has no Projectile sound, which
        // DamagePairs.Paired leaves null; watching it threw ArgumentNullException and the room's entities were not made
        var a = new object();
        var b = new object();
        var starts = LoopStarts.WatchCopies(new[] { a, null, b });
        Assert.Equal(3, starts.Length);
        Assert.Same(LoopStarts.Watch(a), starts[0]);
        Assert.Null(starts[1]);
        Assert.Same(LoopStarts.Watch(b), starts[2]);
    }
}
