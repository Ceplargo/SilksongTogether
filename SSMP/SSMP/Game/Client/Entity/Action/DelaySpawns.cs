using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity.Action;

/// <summary>
/// (coop, 0.4) A copy's SpawnObjectFromGlobalPoolDelay replay waits the scene host's delay, and stopped when the host's
/// FSM left the state. A state of delayed spawns alone (the Widow's Slams Outer: 8 slashes, each 0 to 0.25 s after
/// entry) finishes on its last spawn, so in the host's game the last slash and the state's exit come in one frame; the
/// copy's wait, a frame late at most, then lost the race to the exit now and then, and that slash, a hurt box 2.5 s on the
/// floor, never came in the other game (1 to 4 of about 55 her fight; NOTES 542, the 3c A runs 2026-11-20). As the state
/// is left, a spawn not yet made whose delay was up, give or take <see cref="Tolerance"/>, is made then. Unity-free, so it
/// is tested without the game.
/// </summary>
internal static class DelaySpawns {
    /// <summary>Seconds: the copy's wait a frame late, and the exit's packet a little earlier than the spawn's.</summary>
    public const float Tolerance = 0.1f;

    /// <summary>Whether a copy's delayed spawn is made as the scene host's state is left.</summary>
    public static bool AtExit(bool spawned, float elapsed, float delay) =>
        !spawned && elapsed + Tolerance >= delay && SpawnsAtExit();

    /// <summary>Not inlined: widow_slams' twin skips it, which gives false (the spawn dropped at the exit, as before).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool SpawnsAtExit() => true;
}
