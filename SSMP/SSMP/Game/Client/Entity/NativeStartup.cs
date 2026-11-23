using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity;

internal static class NativeStartup {
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool KeepsHiveRunning(EntityType type) => type == EntityType.ShellwoodHive;

    /// <summary>
    /// (coop, 0.4 enemies A) Whether SSMP leaves the original running while the room's host is not yet known, instead of
    /// switching it off at registration and on again: a hive (it loses its next-frame startup event), and an enemy in an
    /// arena's wave (its BattleScene, not SSMP, decides when it acts). Switched off and on, a wave enemy's FSMs restarted
    /// (PlayMaker's RestartOnEnable) after their first frames had used up their setup: Chapel_Wanderer's Wave 2 Pilgrim 02
    /// went through Battle Wait (Battler set False, its mesh off), then restarted in Idle and Walk, so its wave's BATTLE
    /// START found no state to take it and the wave never ended, undrawn (probe_pilgrim_wave; the game alone without SSMP
    /// brings it out: probe_pilgrim_vanilla, 2026-11-20). Unity-free, so it is tested without the game.
    /// </summary>
    internal static bool KeepsRunning(EntityType type, bool inBattleWave) =>
        KeepsHiveRunning(type) || inBattleWave && KeepsWaveRunning();

    /// <summary>Not inlined: the twin skips it, which gives false (a wave enemy switched off at registration, as
    /// before).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool KeepsWaveRunning() => true;
}
