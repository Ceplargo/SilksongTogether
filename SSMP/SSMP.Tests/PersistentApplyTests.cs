using SSMP.Game.Client.Save;
using Xunit;

namespace SSMP.Tests;

public class PersistentApplyTests {
    [Theory]
    [InlineData(true, false, (int) LeverArrival.Hit)]
    [InlineData(true, true, (int) LeverArrival.Nothing)]
    [InlineData(false, true, (int) LeverArrival.Load)]
    [InlineData(false, false, (int) LeverArrival.Load)]
    public void A_lever_turned_on_elsewhere_plays_its_own_hit_once(bool arriving, bool activated, int action) {
        Assert.Equal((LeverArrival) action, PersistentApply.ForLever(arriving, activated));
    }

    // The Bellshrine: the other game's value loaded its sequence returning, which jumped it to its last state (its
    // lever's hit had set the complete bool), none of the sequence shown (probe_bell_sequence)
    [Theory]
    [InlineData(2, 1, true, (int) SequenceArrival.Play)]
    [InlineData(2, 2, true, (int) SequenceArrival.Nothing)]
    [InlineData(2, 3, true, (int) SequenceArrival.Nothing)]
    [InlineData(2, 1, false, (int) SequenceArrival.Load)]
    public void A_sequence_moved_on_elsewhere_plays_its_states_here(int arriving, int current, bool live, int action) {
        Assert.Equal((SequenceArrival) action, PersistentApply.ForSequence(arriving, current, live));
    }

    // Bone_04's Bone Gate saves itself open as its sequence starts: the other game's value came while this game's
    // gate was still in its lever's delay and loaded it open mid-way
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void A_gate_its_own_lever_is_opening_is_left_to_it(bool arriving, bool leverOn, bool left) {
        Assert.Equal(left, PersistentApply.LeftToItsLever(arriving, leverOn));
    }

    // Bone_18's arena: each game's win reached the other as its saved value, whose load completes an arena at once with
    // a quick gate open, so the host's game, already in its own end, opened its gates 4 s early (probe_arena_end)
    [Theory]
    [InlineData(true, true, false, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    public void An_arena_running_or_ending_here_is_left_to_its_own_end(bool arriving, bool started, bool completed,
        bool left) {
        Assert.Equal(left, PersistentApply.LeftToItsArena(arriving, started, completed));
    }

    // Bone_04's Shell Shard Fossil Tiny Front (2): the friend's hit reached the host's game as its value before the copy
    // of the friend's swing, whose load broke it there with no break shake or sound
    [Theory]
    [InlineData(0, 1, true, true)]
    [InlineData(1, 2, true, true)]
    [InlineData(1, 1, true, false)]
    [InlineData(2, 1, true, false)]
    [InlineData(0, 1, false, false)]
    public void A_holder_hit_elsewhere_plays_its_hits_here(int arriving, int hitsLeft, bool live, bool plays) {
        Assert.Equal(plays, PersistentApply.ForHolder(arriving, hitsLeft, live));
    }

    // Bone_01c's rosary_string_small_half (1): the friend's hit reached the host's game as its value before the copy
    // of the friend's swing, whose load hid the beads there with no fling (Task 12, run 2026-11-10_023548)
    [Theory]
    [InlineData(1, 0, true, true)]
    [InlineData(2, 0, true, true)]
    [InlineData(1, 1, true, false)]
    [InlineData(0, 1, true, false)]
    [InlineData(1, 0, false, false)]
    public void A_cache_hit_elsewhere_is_owed_its_hits_here(int arriving, int state, bool live, bool owed) {
        Assert.Equal(owed, PersistentApply.ForCache(arriving, state, live));
    }
}
