using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Save;

/// <summary>
/// (coop, 0.4 World A) A reload of the same room (a crest's memory return: "Reloading same scene") loads each saved
/// object from this game's SceneData, so a find the game never stored there is lost: Chapel_Wanderer's Remasker New Sharp
/// (1), found at the door by the crest's setter, read False after its reload (as in the game alone) while the other game,
/// whose SceneData SSMP had written, kept True, through a bench save and both games' rejoin (crest-bench-p3). The game
/// now sends the bools its reload changed, so every game and the server's store end with the reloaded value (the
/// project's ruling B, 2026-11-18: both False, as the game alone ends); only those, never the room's whole
/// SceneData, and never a semi-persistent one (its reset travels as a reset). Unity-free, so it is tested without the
/// game.
/// </summary>
internal static class ReloadResend {
    /// <summary>The keys of the objects found again after the reload whose loaded value differs from the one this game
    /// had for them before it (before: key to value; after: key, value, semi-persistent).</summary>
    public static List<TKey> Changed<TKey>(IReadOnlyDictionary<TKey, bool> before, IEnumerable<(TKey Key, bool Value, bool Semi)> after)
        where TKey : notnull =>
        after.Where(a => !a.Semi && before.TryGetValue(a.Key, out var was) && was != a.Value).Select(a => a.Key).ToList();

    /// <summary>The switch the check's twin turns off (a skipped bool method returns false).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool Resends() => true;
}
