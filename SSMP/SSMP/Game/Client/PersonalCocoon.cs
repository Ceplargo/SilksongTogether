using System.Runtime.CompilerServices;

namespace SSMP.Game.Client;

/// <summary>
/// (coop) Hornet's cocoon is its own player's (the design's personal table). Each game spawns only its own player's,
/// from its HeroCorpseScene, at the room's corpse marker, so two players who died in one room have theirs at one spot,
/// and a remote player's replayed needle swing broke the local player's too (bosses Task 8, NOTES 307). GamePatcher
/// ignores remote players' hits on it.
/// </summary>
internal static class PersonalCocoon {
    /// <summary>Whether an object is Hornet's cocoon (CustomSceneManager's heroCorpsePrefab, instantiated).</summary>
    public static bool Is(string name) => name.StartsWith("Hornet Cocoon Corpse");

    /// <summary>Remote players' hits on the local cocoon are ignored. Not inlined: cocoon_own's twin skips it.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool KeepsRemoteHitsOff() => true;
}
