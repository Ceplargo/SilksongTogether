using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity;

/// <summary>
/// (coop, 0.3.21) A possessed enemy's silk is spawned by its FSM (SpawnObjectFromGlobalPool into its Silk variable,
/// then SilkPossession.SetEnemy) and played later (CallMethodProper SilkPossession.PlayPossess). A copy made after the
/// scene host's game spawned the silk (Bone_18's wave Pilgrim 05, out before the other game's copy) never got that
/// spawn, so the copied PlayPossess found no silk: no strands and no possess sound in that game. Such a copy spawns the
/// silk its FSM's own spawn action names, links it to its enemy, then plays it. Unity-free, so it is tested without the
/// game.
/// </summary>
internal static class LateSilk {
    /// <summary>Whether a copied call needs the silk spawned first: it is a SilkPossession call that found no silk.</summary>
    public static bool Needed(bool found, string? behaviour) => !found && behaviour == "SilkPossession";

    /// <summary>The FSM's spawn action that stores into the call's target variable (by name), else default.</summary>
    public static T? Spawner<T>(string? target, IEnumerable<(string? store, T spawn)> spawns) {
        if (string.IsNullOrEmpty(target)) return default;
        foreach (var (store, spawn) in spawns) {
            if (store == target) return spawn;
        }

        return default;
    }

    /// <summary>
    /// Whether the scene host's FSM now in state has its silk out already: state is where the silk's spawn state goes
    /// on FINISHED (the Pilgrim's Spawn Silk, then Pray), so a copy that learns of it (a late joiner gets the stored
    /// state) shows the idle strands too. transitions: the spawn state's (event, to).
    /// </summary>
    public static bool OutIn(string? state, IEnumerable<(string? evt, string? to)> transitions) {
        if (state == null) return false;
        foreach (var (evt, to) in transitions) {
            if (evt == "FINISHED" && to == state) return true;
        }

        return false;
    }

    /// <summary>
    /// Whether the silk's spawn state leaves before its spawn: the events its tests ahead of the spawn would send (the
    /// Pilgrim's Spawn Silk: blackThreadWorld, then Quick Wake, each FINISHED when true), null or empty for none. A
    /// Quick Wake Pilgrim prays with no silk in the game alone (Bone_11's Pilgrim 03, 01 (1) and 03 (2), probe_silks).
    /// </summary>
    public static bool Skipped(IEnumerable<string?> events) {
        foreach (var e in events) {
            if (!string.IsNullOrEmpty(e)) return true;
        }

        return false;
    }

    /// <summary>Whether a late copy spawns its silk. Not inlined: the check's twin skips it.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool Spawns() => true;
}
