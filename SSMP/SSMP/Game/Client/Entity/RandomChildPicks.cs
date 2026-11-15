using System.Collections.Generic;
using System.Linq;

namespace SSMP.Game.Client.Entity;

/// <summary>
/// (coop, 0.3.23) A room's ActivateRandomChild (getChildren) switches on one random child of its object when that object
/// is switched on. SSMP makes each entity's copy beside its original, a child of the same parent, so the pick could land
/// on a copy: Crawl_03's Crypt Worms wait one in each GameObject (n), and the room-running game switched on a copy, which
/// nothing drives: it stood with its hurt box at the worm's place in that game alone, and that worm never came out
/// (crypt_picks: K10's "no hurt box in the host's game", both orders). The pick now leaves out SSMP's copies, as if they
/// were not there. Each game still picks its own; the room runner's original is the one shown.
/// shortcut: only ActivateRandomChild; ActivateRandomChildren, ActivateRandomChildrenTimer and RandomiseEnabledChild pick
/// the same way. Act 1's census (probe_pickers, 2026-11-14, 153 rooms) found none of those three over an entity's
/// original or copy (17 pickers over entities, all ActivateRandomChild: Crypt Worms in Crawl_01, 03, 05 and 09); run
/// the census again when a later act's rooms are reached, and extend this hook to any kind it finds.
/// </summary>
internal static partial class RandomChildPicks {
    /// <summary>The indices of the children a pick may land on: every one that is not a copy. Unity-free, so it is
    /// tested without the game.</summary>
    public static int[] Pickable(IReadOnlyList<bool> isCopy) => Enumerable.Range(0, isCopy.Count).Where(i => !isCopy[i]).ToArray();
}
