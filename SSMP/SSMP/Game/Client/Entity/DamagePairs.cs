using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity;

/// <summary>
/// (coop) An entity's hurt boxes on its original paired with its clone's by their path under each, Unity-free so
/// SSMP.Tests compiles it. By index they paired wrong where the clone lacks one the boss's FSM had let go of: the
/// Phantom's clone has no Pin Projectile, so every box after it took the next one's damage and on/off in the friend's
/// game (her plunge blast 1 for the host's 2, her Dragoon Down switched on with the blast, her catch stab 0 for 1); the
/// Last Judge's clone has 5 of her original's 22, and its Roar Glow damager followed her Swing Attack/Hit3 (boss round
/// five, dragoon_blast and roar_glow).
/// </summary>
internal static class DamagePairs {
    /// <summary>For each of the original's hurt boxes (its path under the original), the index of the clone's with the same
    /// path under the clone, or -1; each of the clone's paired once, in order.</summary>
    public static int[] Align(string[] original, string[] clone) {
        var free = new Dictionary<string, Queue<int>>();
        for (var i = 0; i < clone.Length; i++) {
            if (!free.TryGetValue(clone[i], out var queue)) {
                free[clone[i]] = queue = new Queue<int>();
            }

            queue.Enqueue(i);
        }

        var pairs = new int[original.Length];
        for (var i = 0; i < original.Length; i++) {
            pairs[i] = free.TryGetValue(original[i], out var queue) && queue.Count > 0 ? queue.Dequeue() : -1;
        }

        return pairs;
    }

    // A delayed old activation must be replaced even while the native keeps the same damage state.
    internal static bool RefreshDue(float now, float last) => now - last >= 0.05f && KeepsDamageFresh();

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool KeepsDamageFresh() => true;

    // Entity owns root activation; damage updates may only switch child objects.
    internal static bool CopiesActivation(bool isRoot) => !isRoot || !KeepsRootHidden();

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool KeepsRootHidden() => true;

    // A cancelled state's replay must not reopen a child already followed by DamageHeroComponent.
    internal static bool UsesNativeActivation(bool resetOnExit, bool recursive, bool pairedChild) =>
        resetOnExit && !recursive && pairedChild && KeepsDamageActivation();

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool KeepsDamageActivation() => true;

    /// <summary>(coop) Test-only, set by CoopQA: log unmatched boxes.
    /// </summary>
    internal static bool Logs;

    /// <summary>The boxes Align left without a match, each a line: the original's with none on the clone, then the
    /// clone's with none on the original.</summary>
    public static List<string> Unmatched(string[] original, string[] clone, int[] pairs) {
        var lines = new List<string>();
        for (var i = 0; i < original.Length; i++) {
            if (pairs[i] < 0) {
                lines.Add($"the original's {Named(original[i])} has none on the clone");
            }
        }

        var used = new HashSet<int>(pairs);
        for (var i = 0; i < clone.Length; i++) {
            if (!used.Contains(i)) {
                lines.Add($"the clone's {Named(clone[i])} has none on the original");
            }
        }

        return lines;
    }

    private static string Named(string path) => path.Length == 0 ? "own box" : path;

    /// <summary>Whether the hurt boxes pair by path. Not inlined: the plunge blast's and roar glow's twins skip it, which
    /// gives false: then they pair by index as before.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool ByPath() => true;
}
