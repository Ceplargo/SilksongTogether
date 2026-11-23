using SSMP.Game.Client.Entity;
using Xunit;

namespace SSMP.Tests;

public class TakeoverStatesTests {
    // Coral_02's Pilgrim BellThrower at the takeover (probe_room_cycles, 2026-11-14): its Patrol's walk refused
    [Fact]
    public void Unitys_refused_coroutine_line_is_a_refusal() => Assert.True(TakeoverStates.RefusedCoroutine(
        "Coroutine 'DoCoroutine' couldn't be started because the the game object 'Pilgrim BellThrower' is inactive!"));

    // (0.4) a coroutine started from code, not by name: Unity's line names no coroutine (Bone_01's Bone Goomba, its
    // Crawler walk refused at the takeover; probe_takeover_fly, 2026-11-16_104015), and the goomba stood still
    [Fact]
    public void Unitys_refusal_of_an_unnamed_coroutine_is_a_refusal() => Assert.True(TakeoverStates.RefusedCoroutine(
        "Coroutine couldn't be started because the the game object 'Bone Goomba (1)' is inactive!"));

    [Fact]
    public void Another_error_is_not_a_refusal() => Assert.False(TakeoverStates.RefusedCoroutine(
        "NullReferenceException: Object reference not set to an instance of an object"));

    // a line that only names a coroutine is not Unity's refusal
    [Fact]
    public void A_coroutine_line_that_was_not_refused_is_not_a_refusal() => Assert.False(TakeoverStates.RefusedCoroutine(
        "Coroutine 'DoCoroutine' started"));

    // (0.4) Aqueduct_03's Swamp Mosquito Skinny: Pause, then "Start at Node?" (sets its Idle Point and Fly Target free),
    // then Init State; taken over without it, it flew at its own child
    [Fact]
    public void A_mosquitos_start_at_node_runs_with_the_init_states() =>
        Assert.True(TakeoverStates.InitsLikeDeparents("start at node?"));

    // its other states run as the host's game sends them
    [Theory]
    [InlineData("init state")]
    [InlineData("start idle")]
    [InlineData("idle dart")]
    [InlineData("get node")]
    public void A_mosquitos_other_states_do_not(string state) => Assert.False(TakeoverStates.InitsLikeDeparents(state));

    // a clip the copy still plays, in a state that does not wait on its events: on from the copy's frame
    [Fact]
    public void A_clip_the_copy_still_plays_goes_on_from_the_copys_frame() =>
        Assert.Equal(3, TakeoverStates.PlayOnFrame(true, 3, 12, false));

    // (0.4) Bone_East_24's Fields Flyer held in Call Rise (it moves on at the Call clip's frame-3 event) while its
    // copy, still playing, is at frame 5: played on from there the event never comes, so the clip plays again
    [Fact]
    public void A_clip_the_copy_still_plays_past_the_states_event_plays_again() =>
        Assert.Equal(0, TakeoverStates.PlayOnFrame(true, 5, 8, true));

    // a copy whose clip has ended, in a state that does not wait on its events: its last frame, as before
    [Fact]
    public void A_clip_the_copy_has_ended_stays_on_its_last_frame() =>
        Assert.Equal(11, TakeoverStates.PlayOnFrame(false, 11, 12, false));

    // (0.4) Bone_East_24's Fields Flyer held in Call Rise, its copy's Call clip ended at frame 7 of 8: Call Rise
    // moves on at a frame event of that clip, so it plays again from the start
    [Fact]
    public void A_clip_the_copy_has_ended_plays_again_where_the_state_waits_on_its_events() =>
        Assert.Equal(0, TakeoverStates.PlayOnFrame(false, 7, 8, true));

    // the original's clip of that name is shorter than the copy's frame
    [Fact]
    public void A_frame_past_the_clip_is_its_last() => Assert.Equal(7, TakeoverStates.PlayOnFrame(true, 10, 8, false));

    // (0.4) Ant_08's Bone Hunter Child taken over in Enter Fall, drawn in its copy: the init states hid the original
    // (its Dormant's SetMeshRenderer off), and Enter Fall never shows it again
    [Fact]
    public void An_original_hidden_by_its_init_states_is_drawn_as_its_copy() => Assert.True(TakeoverStates.Drawn(true, false));

    // a copy the runner's game had hidden (a Child still in Jump In Ready): the original stays hidden
    [Fact]
    public void An_original_whose_copy_was_hidden_stays_hidden() => Assert.False(TakeoverStates.Drawn(false, true));
}
