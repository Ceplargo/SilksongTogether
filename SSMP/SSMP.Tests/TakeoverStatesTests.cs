using SSMP.Game.Client.Entity;
using Xunit;

namespace SSMP.Tests;

public class TakeoverStatesTests {
    // Coral_02's Pilgrim BellThrower at the takeover (probe_room_cycles, 2026-11-14): its Patrol's walk refused
    [Fact]
    public void Unitys_refused_coroutine_line_is_a_refusal() => Assert.True(TakeoverStates.RefusedCoroutine(
        "Coroutine 'DoCoroutine' couldn't be started because the the game object 'Pilgrim BellThrower' is inactive!"));

    [Fact]
    public void Another_error_is_not_a_refusal() => Assert.False(TakeoverStates.RefusedCoroutine(
        "NullReferenceException: Object reference not set to an instance of an object"));

    // a line that only names a coroutine is not Unity's refusal
    [Fact]
    public void A_coroutine_line_that_was_not_refused_is_not_a_refusal() => Assert.False(TakeoverStates.RefusedCoroutine(
        "Coroutine 'DoCoroutine' started"));
}
