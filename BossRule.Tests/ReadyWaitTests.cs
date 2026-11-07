using Xunit;

namespace BossRule.Tests;

public class ReadyWaitTests {
    [Fact]
    public void A_fight_with_every_game_ready_goes() {
        var wait = new ReadyWait();
        wait.Ready(2, "Greymoor_05");
        Assert.True(wait.MayGo("Greymoor_05", new ushort[] { 2 }, 0f));
    }

    [Fact]
    public void Alone_in_the_arena_goes() => Assert.True(new ReadyWait().MayGo("Greymoor_05", new ushort[0], 0f));

    [Fact]
    public void A_game_without_the_boss_holds_the_start_until_the_timeout() {
        var wait = new ReadyWait();
        Assert.False(wait.MayGo("Greymoor_05", new ushort[] { 2 }, 10f));
        Assert.False(wait.MayGo("Greymoor_05", new ushort[] { 2 }, 14.9f));
        Assert.True(wait.MayGo("Greymoor_05", new ushort[] { 2 }, 15f));
        Assert.Equal(new ushort[] { 2 }, wait.Missing("Greymoor_05", new ushort[] { 2 }));
    }

    [Fact]
    public void Ready_for_another_room_does_not_count() {
        var wait = new ReadyWait();
        wait.Ready(2, "Greymoor_08");
        Assert.False(wait.MayGo("Greymoor_05", new ushort[] { 2 }, 0f));
    }

    [Fact]
    public void A_player_who_leaves_the_room_is_forgotten() {
        var wait = new ReadyWait();
        wait.Ready(2, "Greymoor_05");
        wait.Forget(2);
        Assert.False(wait.MayGo("Greymoor_05", new ushort[] { 2 }, 0f));
    }

    [Fact]
    public void A_player_who_leaves_during_the_hold_lets_it_go() {
        var wait = new ReadyWait();
        Assert.False(wait.MayGo("Greymoor_05", new ushort[] { 2 }, 0f));
        Assert.True(wait.MayGo("Greymoor_05", new ushort[0], 1f));
    }

    [Fact]
    public void A_new_hold_counts_its_timeout_again() {
        var wait = new ReadyWait();
        Assert.False(wait.MayGo("Greymoor_05", new ushort[] { 2 }, 0f));
        wait.Restart();
        Assert.False(wait.MayGo("Greymoor_05", new ushort[] { 2 }, 6f));
        Assert.True(wait.MayGo("Greymoor_05", new ushort[] { 2 }, 11f));
    }

    [Fact]
    public void Clear_forgets_every_room() {
        var wait = new ReadyWait();
        wait.Ready(2, "Greymoor_05");
        wait.Clear();
        Assert.False(wait.MayGo("Greymoor_05", new ushort[] { 2 }, 0f));
    }
}
