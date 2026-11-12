using SSMP.Game.Client.Entity.Component;
using Xunit;

namespace SSMP.Tests;

public class FlungThrowsTests {
    // the project, 2026-11-10 19:23: the Rock Roller's wish item falls and lands the same way in every game
    [Fact]
    public void A_rock_rollers_corpse_flings_its_wish_pickup() {
        Assert.True(FlungThrows.IsPickup("Rock Roller", "Corpse Rock Roller(Clone)", "Rock Roller_item_Rock Rollers"));
        Assert.False(FlungThrows.IsPickup("Rock Roller", "Corpse Rock Roller(Clone)", "Rock Roller Bomb(Clone)"));
        Assert.False(FlungThrows.IsPickup("Bone Goomba", "Corpse Rock Roller(Clone)", "Rock Roller_item_Rock Rollers"));
    }

    [Fact]
    public void A_pickup_kind_is_not_a_throws() {
        Assert.True(FlungThrows.IsPickupKind(FlungThrows.PickupKind));
        Assert.False(FlungThrows.IsPickupKind(0));
    }

    [Fact]
    public void The_hosts_pickup_coming_first_waits_for_this_games() {
        var pairs = new PickupPairs<string>();
        Assert.Null(pairs.Host(7));
        Assert.Equal((byte) 7, pairs.Own("own"));
    }

    [Fact]
    public void This_games_pickup_coming_first_waits_for_the_hosts() {
        var pairs = new PickupPairs<string>();
        Assert.Null(pairs.Own("first"));
        Assert.Null(pairs.Own("second"));
        Assert.Equal("first", pairs.Host(3));
        Assert.Equal("second", pairs.Host(4));
        Assert.Null(pairs.Host(5));
    }

    // the scene host left before its pickup's number came (Bone_07): this game's own, held back, is its own again
    [Fact]
    public void A_hand_over_gives_back_this_games_unpaired_pickups() {
        var pairs = new PickupPairs<string>();
        Assert.Null(pairs.Own("held"));
        Assert.Equal(["held"], pairs.Clear());
        Assert.Empty(pairs.Clear());
        Assert.Null(pairs.Host(2));
        Assert.Empty(pairs.Clear());
        Assert.Null(pairs.Own("after"));
    }

    // rag_drop: a Pilgrim's rag is the scene host's roll in every game; a Rock Roller's bombs are not drops
    [Fact]
    public void The_Marrows_Pilgrims_drops_are_followed() {
        Assert.True(FlungThrows.DropsFollowed("Pilgrim 01"));
        Assert.True(FlungThrows.DropsFollowed("Pilgrim Fly"));
        Assert.False(FlungThrows.DropsFollowed("Rock Roller"));
        Assert.True(FlungThrows.IsDropKind(FlungThrows.DropKind));
        Assert.False(FlungThrows.IsDropKind(FlungThrows.PickupKind));
    }

    // the project 22:07: only item drops go out; a rosary or shell shard fling stays each game's own
    [Fact]
    public void A_money_fling_sends_nothing() {
        Assert.False(FlungThrows.SendsFling(false, null));
        Assert.False(FlungThrows.SendsFling(false, 0));
        Assert.False(FlungThrows.SendsFling(true, null));
        Assert.True(FlungThrows.SendsFling(true, 2));
    }

    [Fact]
    public void A_drops_index_runs_across_its_groups_and_back() {
        string[][] table = [["Shell Shard"], ["Rosary", "Pilgrim Rag"]];
        Assert.Equal((byte) 2, FlungThrows.DropIndex(table, d => d == "Pilgrim Rag"));
        Assert.Equal("Pilgrim Rag", FlungThrows.DropAt(table, 2));
        Assert.Null(FlungThrows.DropIndex(table, d => d == "Mossberry"));
        Assert.Null(FlungThrows.DropAt(table, 3));
    }

    [Fact]
    public void The_Widows_slashes_fling_her_bells() {
        Assert.True(FlungThrows.Has("Spinner Boss"));
        Assert.Equal("Spinner AtkBell", FlungThrows.Flung("Spinner Boss"));
    }

    [Fact]
    public void Both_her_vertical_slashes_are_her_throws() {
        Assert.True(FlungThrows.ThrownBy("Spinner Boss", "Spinner_vertical_slash_long_new(Clone)"));
        Assert.True(FlungThrows.ThrownBy("Spinner Boss", "Spinner_vertical_slash(Clone)"));
    }

    [Fact]
    public void Her_harp_shots_are_her_throws_too() {
        // each shot moves to a random place before it flings its bell (Pos L/R: RandomFloat, SetPosition)
        Assert.True(FlungThrows.ThrownBy("Spinner Boss", "Spinner BellShot 1"));
        Assert.True(FlungThrows.ThrownBy("Spinner Boss", "Spinner BellShot 2"));
    }

    [Fact]
    public void Other_objects_and_enemies_are_not() {
        Assert.False(FlungThrows.ThrownBy("Spinner Boss", "Spinner Boss"));
        Assert.False(FlungThrows.ThrownBy("Bone Beast", "Spinner_vertical_slash(Clone)"));
        Assert.False(FlungThrows.Has("Bone Beast"));
        Assert.Null(FlungThrows.Flung("Bone Beast"));
    }

    [Fact]
    public void The_Last_Judges_charge_drops_her_bombs() {
        // her Charge spawns one at her Bomb Point, then one every 0.2 s (SpawnObjectFromGlobalPoolOverTime)
        Assert.True(FlungThrows.Has("Last Judge"));
        Assert.Equal("Last Judge Charge Bomb", FlungThrows.Flung("Last Judge"));
        Assert.True(FlungThrows.ThrownBy("Last Judge", "Last Judge"));
    }

    [Fact]
    public void Only_what_is_flung_is_followed() {
        Assert.True(FlungThrows.IsFlungObject("Last Judge", "Last Judge Charge Bomb(Clone)"));
        Assert.True(FlungThrows.IsFlungObject("Spinner Boss", "Spinner AtkBell(Clone)"));
        Assert.False(FlungThrows.IsFlungObject("Last Judge", "Last Judge Stomp Wave(Clone)"));
        Assert.False(FlungThrows.IsFlungObject("Bone Beast", "Last Judge Charge Bomb(Clone)"));
    }

    [Fact]
    public void A_throw_gets_one_copy_until_the_host_ends_it() {
        // the Last Judge's bomb copy recycled itself before the host's did; the next update still listed the host's,
        // and a second copy put straight into its flare hurt for 0.5 s where the host's game had none (NOTES 339)
        Assert.True(FlungThrows.NeedsCopy(hasCopy: false, copiedBefore: false));
        Assert.False(FlungThrows.NeedsCopy(hasCopy: false, copiedBefore: true));
        Assert.False(FlungThrows.NeedsCopy(hasCopy: true, copiedBefore: true));
    }

    [Fact]
    public void A_Rock_Rollers_corpse_throws_its_death_bombs() {
        // its Control FSM's Explode: 3 times SpawnObjectFromGlobalPool, then SetVelocity2d at a random speed
        Assert.True(FlungThrows.Has("Rock Roller"));
        Assert.Equal("Rock Roller Bomb", FlungThrows.Flung("Rock Roller"));
        Assert.True(FlungThrows.ThrownBy("Rock Roller", "Corpse Rock Roller(Clone)"));
        Assert.True(FlungThrows.IsFlungObject("Rock Roller", "Rock Roller Bomb(Clone)"));
    }

    [Fact]
    public void A_living_Rock_Rollers_own_bombs_stay_its_throws() {
        // its fight throws spawn the same bomb from its own FSM: followed by number, not as flung
        Assert.False(FlungThrows.ThrownBy("Rock Roller", "Rock Roller"));
        Assert.False(FlungThrows.ThrownBy("Rock Roller", "Rock Roller(Clone)"));
    }

    [Fact]
    public void Only_the_marker_says_flung() {
        Assert.True(FlungThrows.IsFlung(FlungThrows.Marker));
        Assert.False(FlungThrows.IsFlung(255));
        Assert.False(FlungThrows.IsFlung(0));
    }
}
