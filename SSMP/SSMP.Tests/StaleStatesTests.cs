using System.Collections;
using System.Collections.Generic;
using SSMP.Game.Client.Entity.Action;
using Xunit;

namespace SSMP.Tests;

public class StaleStatesTests {
    // the Swamp Ductsucker's way up and the Skull King's jump, their transitions
    private static readonly Dictionary<string, string[]> Graph = new() {
        ["Emerge"] = new[] { "Start Choice" }, ["Start Choice"] = new[] { "Charge Start" },
        ["Charge Start"] = new[] { "Charge" }, ["Charge"] = new[] { "Jump Antic F", "Skid" },
        ["Jump Antic F"] = new[] { "Jump" }, ["Jump"] = new[] { "Land" }, ["Land"] = new[] { "Idle" },
        ["Idle"] = new[] { "Jump Launch", "Emerge" }, ["Jump Launch"] = new[] { "Jump Air" }, ["Jump Air"] = new[] { "Land" },
    };

    private static int Steps(string from, string to) =>
        StaleStates.Steps(from, to, s => Graph.TryGetValue(s, out var n) ? n : new string[0], new string[0]);

    // the Swamp Ductsucker: Charge's action came in the update whose state was already Jump Antic F
    [Fact]
    public void A_state_left_in_the_same_frame_ends_at_once() =>
        Assert.True(StaleStates.Left("Jump Antic F", 120, "Charge", 120, Steps));

    // the Skull King: the host read Jump Launch, then entered Jump Air later that frame; it is in Jump Air
    [Fact]
    public void A_state_entered_after_the_hosts_read_runs_on() =>
        Assert.False(StaleStates.Left("Jump Launch", 120, "Jump Air", 120, Steps));

    [Fact]
    public void The_state_it_is_in_runs_on() => Assert.False(StaleStates.Left("Charge", 120, "Charge", 120, Steps));

    // its state not heard of yet: the last state came in an earlier frame
    [Fact]
    public void An_earlier_frames_state_does_not_end_it() =>
        Assert.False(StaleStates.Left("Emerge", 119, "Charge", 120, Steps));

    [Fact]
    public void No_state_yet_does_not_end_it() => Assert.False(StaleStates.Left(null, -1, "Charge", 120, Steps));

    // unclear which came first: the old behavior (it runs on to the next state change)
    private static int Loop(string from, string to) => StaleStates.Steps(from, to, s => s switch {
        "A" => new[] { "B" }, "B" => new[] { "C" }, "C" => new[] { "A" }, "D" => new[] { "E" }, "E" => new[] { "D" },
        _ => new string[0]
    }, new string[0]);

    [Fact]
    public void A_tie_runs_on() => Assert.False(StaleStates.Left("E", 120, "D", 120, Loop));

    [Fact]
    public void A_loop_back_to_the_hosts_state_runs_on() => Assert.False(StaleStates.Left("A", 120, "B", 120, Loop));

    [Fact]
    public void No_path_either_way_runs_on() => Assert.False(StaleStates.Left("Nowhere", 120, "Charge", 120, Steps));

    [Fact]
    public void Steps_count_the_fewest_transitions() {
        Assert.Equal(1, Steps("Charge", "Jump Antic F"));
        Assert.Equal(7, Steps("Jump Antic F", "Charge"));
        Assert.Equal(int.MaxValue, Steps("Charge", "Nowhere"));
        Assert.Equal(1, StaleStates.Steps("Charge", "Idle", _ => new string[0], new[] { "Idle" }));
    }

    // the Ductsucker's charge box replayed as Unity starts a coroutine: its entry runs to the first wait, then it registers
    private static (List<string> Log, bool Kept) Replay(bool left) {
        var log = new List<string>();
        var stopped = false;
        IEnumerator Body() {
            log.Add("entry");
            yield return null;
            log.Add("rest");
        }

        var routine = Body();
        routine.MoveNext();
        var kept = StaleStates.Keeps(left, () => stopped = true, () => log.Add("exit"));
        if (!stopped) routine.MoveNext();
        return (log, kept);
    }

    [Fact]
    public void A_stale_replay_keeps_its_entry_and_ends_its_rest() {
        var (log, kept) = Replay(true);
        Assert.Equal(new[] { "entry", "exit" }, log);
        Assert.False(kept);
    }

    [Fact]
    public void A_current_replay_runs_on() {
        var (log, kept) = Replay(false);
        Assert.Equal(new[] { "entry", "rest" }, log);
        Assert.True(kept);
    }
}
