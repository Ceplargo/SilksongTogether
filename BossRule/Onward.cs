using System.Collections.Generic;
using System.Linq;

namespace BossRule;

/// <summary>
/// What of an arena a held fight shuts: the catalog's onward objects named for the door this game's hero came in by
/// (round three's walk-past sweep found each one open while the fight waited, as only the fight's start shuts it).
/// The way in is never among them, so nobody is shut outside (spec section 3, rule 2), in the second player's game
/// either; without a known door nothing is shut. Unity-free.
/// </summary>
internal static class Onward {
    public static IReadOnlyList<string> Shut(IReadOnlyList<BossOnward> onward, string? entryGate) =>
        entryGate == null ? [] : onward.Where(o => o.From.Contains(entryGate)).Select(o => o.Object).ToList();
}
