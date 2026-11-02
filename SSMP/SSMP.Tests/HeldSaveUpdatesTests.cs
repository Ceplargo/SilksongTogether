using System;
using System.Linq;
using SSMP.Game.Client.Save;
using Xunit;

namespace SSMP.Tests;

public class HeldSaveUpdatesTests {
    [Fact]
    public void Numbers_go_up_from_the_start_time() {
        var before = (ulong) DateTime.UtcNow.Ticks;
        var held = new HeldSaveUpdates();
        var first = held.Add(1, [1], false, 0);
        Assert.True(first > before);
        Assert.Equal(first + 1, held.Add(2, [2], false, 0));
    }

    [Fact]
    public void Only_the_last_minute_is_held() {
        var held = new HeldSaveUpdates();
        held.Connected(7);
        held.Add(1, [1], false, 0);
        held.Add(2, [2], false, 61);
        Assert.Equal([2], held.ForRejoin()!.Value.Updates.Select(u => (int) u.Index));
    }

    [Fact]
    public void A_value_keeps_its_latest_and_deltas_all() {
        var held = new HeldSaveUpdates();
        held.Connected(7);
        held.Add(5, [1], false, 0);
        var d1 = held.Add(7, [10], true, 1);
        held.Add(5, [2], false, 2);
        var d2 = held.Add(7, [20], true, 3);
        var last = held.Add(5, [3], false, 4);
        var updates = held.ForRejoin()!.Value.Updates;
        Assert.Equal([d1, d2, last], updates.Select(u => u.Number));
        Assert.Equal([10, 20, 3], updates.Select(u => (int) u.Value[0]));
    }

    [Fact]
    public void Held_updates_go_only_to_the_run_they_were_made_in() {
        var held = new HeldSaveUpdates();
        held.Add(1, [1], false, 0);
        Assert.Null(held.ForRejoin());
        held.Connected(42);
        held.Add(1, [1], false, 0);
        Assert.Equal(42UL, held.ForRejoin()!.Value.RunId);
    }

    [Fact]
    public void A_held_update_keeps_its_claim() {
        // A rejoin sends the one-of-a-kind action's held changes again with its marker, so the server knows them as
        // the same player's (decision "The lock")
        var held = new HeldSaveUpdates();
        held.Connected(7);
        held.Add(5, [1], true, 0, "pb:Bone_East_20:Heart Piece");
        held.Add(6, [1], false, 1);
        Assert.Equal(["pb:Bone_East_20:Heart Piece", ""], held.ForRejoin()!.Value.Updates.Select(u => u.Claim));
    }

    [Fact]
    public void Connected_forgets_what_was_held() {
        var held = new HeldSaveUpdates();
        held.Connected(1);
        held.Add(1, [1], false, 0);
        held.Connected(2);
        Assert.Null(held.ForRejoin());
    }
}
