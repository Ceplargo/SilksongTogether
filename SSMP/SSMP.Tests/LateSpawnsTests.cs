using SSMP.Game.Client.Entity;
using Xunit;

namespace SSMP.Tests;

public class LateSpawnsTests {
    // a boss scene loaded 9 to 17 s after the room: Moorwing's sickles from then were replayed at once (boss_late_throws)
    [Fact]
    public void An_update_that_waited_seconds_for_its_entity_is_late() => Assert.True(LateSpawns.Late(9f));

    // a room's own entities register a few frames after their first updates: not late
    [Fact]
    public void An_update_that_waited_a_few_frames_is_not_late() => Assert.False(LateSpawns.Late(0.05f));

    [Fact]
    public void An_update_that_waited_exactly_the_limit_is_not_late() => Assert.False(LateSpawns.Late(LateSpawns.AfterS));

    // an ended sickle the scene host numbered: its next thrown update brings back any still out
    [Fact]
    public void A_late_followed_throw_is_left_out() => Assert.True(LateSpawns.Skips(true, 3, true));

    // the project (MR6): a hazard, bomb or puddle nothing follows would be missing for good
    [Fact]
    public void A_late_spawn_nothing_follows_still_plays() => Assert.False(LateSpawns.Skips(true, 0, true));

    // the scene host numbered it, but this entity has no ThrownComponent to bring it back
    [Fact]
    public void A_late_spawn_on_an_entity_that_follows_no_throws_still_plays() => Assert.False(LateSpawns.Skips(true, 3, false));

    [Fact]
    public void A_followed_throw_on_time_plays() => Assert.False(LateSpawns.Skips(false, 3, true));
}
