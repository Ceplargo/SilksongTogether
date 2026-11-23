using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity;

/// <summary>
/// (coop, 0.3.23) A takeover (Entity.MakeHost) enters each FSM's state from the scene host's game while the original is
/// still hidden (inactive), then switches it on. Two things went wrong in Coral_02 when the host left (takeover_coral):
/// <list type="bullet">
/// <item>The hanging Blade Spider and the Pilgrim 01 Judge Buddy wait in Ambush Ready, which holds them kinematic. The
/// takeover entered that state, then set every body by TakeoverBodies' rule, which made them dynamic (each has a solid
/// collider): both fell through the floor and died below the room. The body is now set before the states are entered,
/// so a state that sets it has the last word, as in the game.</item>
/// <item>The Pilgrim BellThrower's Patrol walks by a coroutine (WalkLeftRight). Started on the inactive original, Unity
/// refused it and logged an error (no exception, so it was not entered again once the original was on), and it stood
/// still. A state whose coroutine was refused is now entered again after the original is switched on, as a state that
/// threw while inactive already was.</item>
/// </list>
/// Unity-free, so it is tested without the game.
/// shortcut: a refusal is read from Unity's log text; if Unity ever words it differently, re-entry stops (takeover_coral
/// fails on a BellThrower standing still).
/// </summary>
internal static class TakeoverStates {
    /// <summary>
    /// Whether a log line is Unity refusing a coroutine on an inactive object. Unity names the coroutine only when it
    /// was started by name ("Coroutine 'DoCoroutine' couldn't be started because ..."); one started from code reads
    /// "Coroutine couldn't be started because ..." (0.4: a Crawler's walk, so Bone_01's goombas stood still).
    /// </summary>
    public static bool RefusedCoroutine(string message) =>
        message.StartsWith("Coroutine ") && message.Contains(" couldn't be started because ");

    /// <summary>Not inlined: takeover_coral's twin skips it, which gives false (the body set after the states).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool SetsBodyFirst() => true;

    /// <summary>Not inlined: takeover_coral's twin skips it, which gives false (a refused state not entered again).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool EntersRefusedAgain() => true;

    /// <summary>
    /// (coop, 0.4) A velocity a state's entry sets on the hidden original is lost when it is made active: Aqueduct_01's
    /// Swamp Ductsuckers, taken over in Suck R (a SetVelocity2d of 0.76 on entry, then only position checks), had
    /// velocity 0 and stood still (`probe_takeover_fly`, 2026-11-16). Once active, each taken-over state's velocity
    /// actions run their entry again. Not inlined: takeover_rooms' twin skips it, which gives false.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool SetsVelocityAgain() => true;

    /// <summary>
    /// (coop, 0.4) A copy made at the scene's root can be moved under a parent later (its FSM's SetParent, copied from
    /// the host: Crawl_07's Crypt Worms under Area_States/Uninfected at (81.0, 38.7)). The takeover took the copy's local
    /// place for its world place, so a worm taken over went from (6.8, 35.9) to (-74.2, -2.8), out of its room (the
    /// takeover pass, 3 of 3; probe_takeover_fly). The original now goes to the copy's world place. Not inlined:
    /// takeover_rooms' twin skips it, which gives false.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool PlacesByWorld() => true;

    /// <summary>
    /// (coop, 0.4) Whether a state of this lower-case name runs with the init states (EntityInitializer), after
    /// "deparents": a Swamp Mosquito's "Start at Node?" sets its Idle Point and Fly Target free of it, and its Idle Dart
    /// flies at the Idle Point. The hidden original never ran it, so taken over, its Idle Point was still its child and
    /// it flew one way, 14 units in 15 s, until it went (Aqueduct_03's Swamp Mosquito Skinny (6), the takeover pass 3 of
    /// 3; probe_takeover_fly). Not inlined: takeover_rooms' twin skips DetachesNodePoints, which gives false.
    /// </summary>
    public static bool InitsLikeDeparents(string state) => state == "start at node?" && DetachesNodePoints();

    /// <summary>(coop, 0.4) The switch of InitsLikeDeparents.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool DetachesNodePoints() => true;

    /// <summary>
    /// (coop, 0.4) The frame a taken-over original's play-once clip plays from: its start where the taken-over state
    /// waits on the clip's events; else the copy's own frame while the copy still plays it, or the last frame (as
    /// before). A Fields Flyer's Call Antic and Call Rise move on at frame events of its Call clip (Call Rise at frame
    /// 3; CoopQA calllog). The runner's game stops running the room as its hero leaves, so its flyer is held mid-call,
    /// while the copy plays on: past frame 3, or to the clip's end (probe_takeover_fly: the original in Call Rise, its
    /// clip ended at frame 7). From the copy's frame or the last frame no event came and it stood still in that state
    /// (Bone_East_24, the takeover pass, 3 of 6 runs and 2 of 10 repeats; the control 0 of 3). From the start, an event
    /// can come early (a flyer held in Call Rise moves on at Call Antic's event), not never.
    /// </summary>
    public static int PlayOnFrame(bool copyPlaying, int copyFrame, int frames, bool waitsOnEvents) =>
        waitsOnEvents ? 0 : copyPlaying ? System.Math.Min(copyFrame, frames - 1) : frames - 1;

    /// <summary>(coop, 0.4) Not inlined: takeover_rooms' twin skips it, which gives false (PlayOnFrame not used).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool PlaysOnFromCopy() => true;

    /// <summary>
    /// (0.4) Not inlined: takeover_rooms' twin skips it, which gives false (the init states run after the original is
    /// moved to the copy's place, as before; Entity.MakeHost).
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool InitsAtSpawn() => true;

    /// <summary>
    /// (coop, 0.4) Whether a taken-over original is drawn: as its copy was. The copy followed the runner's renderer
    /// updates, while the original ran the init states (EntityInitializer), which can hide it: a Bone Hunter Child's
    /// Dormant turns its mesh renderer off, and taken over in Enter Fall (past Jump In, its renderer-on) it acted and hurt
    /// for 18 s undrawn in both games (Ant_08, the arena pass's "lead rejoins"; probe_child_takeover, 2026-11-20).
    /// </summary>
    public static bool Drawn(bool copyDrawn, bool originalDrawn) => DrawsAsCopy() ? copyDrawn : originalDrawn;

    /// <summary>(coop, 0.4) Not inlined: takeover_child's twin skips it, which gives false (Drawn not used).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool DrawsAsCopy() => true;
}
