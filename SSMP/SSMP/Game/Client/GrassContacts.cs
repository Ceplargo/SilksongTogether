using System.Collections.Generic;

namespace SSMP.Game.Client;

/// <summary>
/// (coop) Which grass another player's copy goes into and out of this frame, from the grass it overlaps now and what it
/// overlapped the frame before (RemoteGrass). Unity-free: objects by their instance IDs.
/// </summary>
internal static class GrassContacts {
    /// <summary>
    /// The IDs in now that were not in inside (entered) and those in inside no longer in now (left); inside becomes now.
    /// </summary>
    internal static (List<int> entered, List<int> left) Step(HashSet<int> inside, IReadOnlyCollection<int> now) {
        var entered = new List<int>();
        foreach (var id in now) {
            if (inside.Add(id)) entered.Add(id);
        }

        var left = new List<int>();
        foreach (var id in inside) {
            if (!System.Linq.Enumerable.Contains(now, id)) left.Add(id);
        }

        foreach (var id in left) inside.Remove(id);
        return (entered, left);
    }
}
