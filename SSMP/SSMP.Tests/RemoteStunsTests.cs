using SSMP.Game.Client.Entity.Component;
using Xunit;

namespace SSMP.Tests;

public class RemoteStunsTests {
    [Fact]
    public void A_copied_swing_stuns_an_enemy_once() {
        // the combined 0.3.15 gate: the friend's copied swing hit Mossbone Mother twice in one frame in the scene host's
        // game, her stun counted two and she staggered one friend hit early
        var stuns = new RemoteStuns();
        Assert.True(stuns.First(enemy: 1, swing: 7, now: 10.0));
        Assert.False(stuns.First(enemy: 1, swing: 7, now: 10.0));
    }

    [Fact]
    public void Another_swing_on_the_same_enemy_stuns_it_too() {
        // two players' copied swings landing in one frame both count
        var stuns = new RemoteStuns();
        Assert.True(stuns.First(1, 7, 10.0));
        Assert.True(stuns.First(1, 8, 10.0));
    }

    [Fact]
    public void The_same_swing_stuns_another_enemy() {
        var stuns = new RemoteStuns();
        Assert.True(stuns.First(1, 7, 10.0));
        Assert.True(stuns.First(2, 7, 10.0));
    }

    [Fact]
    public void A_swing_is_forgotten_once_Keep_has_passed() {
        var stuns = new RemoteStuns();
        Assert.True(stuns.First(1, 7, 10.0));
        Assert.False(stuns.First(1, 7, 10.0 + RemoteStuns.Keep - 0.1));
        Assert.True(stuns.First(1, 7, 10.0 + RemoteStuns.Keep + 0.1));
    }
}
