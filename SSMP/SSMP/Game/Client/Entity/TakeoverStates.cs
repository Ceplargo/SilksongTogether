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
    /// <summary>Whether a log line is Unity refusing a coroutine on an inactive object.</summary>
    public static bool RefusedCoroutine(string message) =>
        message.StartsWith("Coroutine '") && message.Contains("' couldn't be started because ");

    /// <summary>Not inlined: takeover_coral's twin skips it, which gives false (the body set after the states).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool SetsBodyFirst() => true;

    /// <summary>Not inlined: takeover_coral's twin skips it, which gives false (a refused state not entered again).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool EntersRefusedAgain() => true;
}
