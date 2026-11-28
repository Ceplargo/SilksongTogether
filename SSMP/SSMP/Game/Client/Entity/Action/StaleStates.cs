using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity.Action;

/// <summary>
/// (coop, 0.4) A copy applies an update's scene-host state before that update's actions. A state the host left within one
/// frame (the Swamp Ductsucker's Charge, which sends JUMP on entry) is gone by then, so its in-state replay (a delayed
/// switch, an in-state emitter or sound) registered after the state change it waits for and ran until the next one: the
/// Ductsucker's 2-damage Collider Charge on for up to 0.9 s in the other game, none in the host's (3c B, AQUEDUCT,
/// probe_ductsucker_charge, 2026-11-20). Such a replay ends as it starts. An action whose state the copy has not heard of
/// yet (its state came in an earlier frame) runs as before. The host reads its states in its Update, so a state entered later
/// in that frame (an animation's end, a coroutine) comes with the earlier state: the update has Jump Launch's state and Jump
/// Air's actions, the host still in Jump Air (Skull King, Crowman Juror, Dock Guard Thrower; plan2 at lag 50, 2026-11-21).
/// So a replay ends only when the host's state comes after its own in the FSM's graph (fewer transitions from its state to
/// the host's than back). Unity-free, so it is tested without the game.
/// </summary>
internal static class StaleStates {
    /// <summary>Whether an in-state replay of <paramref name="state"/> ends at once: another state, one that comes after
    /// it (<paramref name="steps"/>: transitions from one state to another), was registered for its FSM in this same
    /// frame.</summary>
    public static bool Left(string? lastState, int lastFrame, string state, int frame, Func<string, string, int> steps) =>
        lastState != null && lastFrame == frame && lastState != state &&
        steps(state, lastState) < steps(lastState, state) && EndsAtOnce();

    /// <summary>The fewest transitions from <paramref name="from"/> to <paramref name="to"/> (<paramref name="next"/>:
    /// a state's transitions' targets; a global transition's target is one from any state); int.MaxValue if none.</summary>
    public static int Steps(string from, string to, Func<string, IEnumerable<string>> next, ICollection<string> globals) {
        if (globals.Contains(to)) return 1;
        var seen = new HashSet<string> { from };
        var ring = new List<string> { from };
        for (var n = 1; ring.Count > 0; n++) {
            var outer = new List<string>();
            foreach (var s in ring) {
                foreach (var t in next(s)) {
                    if (t == to) return n;
                    if (t != null && seen.Add(t)) outer.Add(t);
                }
            }

            ring = outer;
        }

        return int.MaxValue;
    }

    /// <summary>
    /// A replay registering for its state: its entry ran already (its apply, its coroutine's first step: the host's one
    /// frame in the state happened). A stale one has only its in-state rest ended: its coroutine stopped, then its exit
    /// action (a reset on exit, an emitter stopped). Whether it is kept for the next state change.
    /// <paramref name="runsOut"/> (<see cref="RunsOut"/>): a stale one is not kept, and its rest runs on, ended by no
    /// state change.
    /// </summary>
    public static bool Keeps(bool left, bool runsOut, System.Action stopRest, System.Action? exit) {
        if (!left) return true;
        if (runsOut) return false;
        stopRest();
        exit?.Invoke();
        return false;
    }

    /// <summary>
    /// (0.4.1) Whether a stale replay runs out its rest: a replay of delayed spawns (<paramref name="delayedSpawns"/>).
    /// Its state was left in this update, yet the host could have been in it a while: the update came bunched in transit,
    /// as the Widow's Slams Outer (all 8 slashes, 0 to 0.25 s, spawned in the host's game) came in one frame with
    /// DashSlash Antic, and 7 of the 8 never came in the friend's game (0.4.1 gate, widow_slams, 2026-11-23). Each
    /// spawn is then made after the host's delay.
    /// </summary>
    public static bool RunsOut(bool delayedSpawns) => delayedSpawns && RunsOutDelays();

    /// <summary>Not inlined: widow_slams' twin skips it, which gives false (a stale one's spawns stopped, as in 0.4.0).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool RunsOutDelays() => true;

    /// <summary>Not inlined: ductsucker_charge's twin skips it, which gives false (the replay runs on, as before).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool EndsAtOnce() => true;
}
