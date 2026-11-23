using System.Collections.Generic;

namespace SSMP.Game.Client.Save;

/// <summary>
/// (coop, optimization fix B) A compound save value's change hash: its items' hash codes XORed, 0 for none or null,
/// the same number SaveManager's LINQ hash gave, read with no allocation. The four HashSet values were copied into a
/// new list every frame for it (528 MB in a 30-minute co-op walk, mini PC job 25), and each list's hash built a LINQ
/// iterator.
/// </summary>
internal static class ListHash {
    public static int Of<T>(List<T>? list) {
        if (list == null) return 0;
        var hash = 0;
        for (var i = 0; i < list.Count; i++) hash ^= list[i]?.GetHashCode() ?? 0;
        return hash;
    }

    public static int Of<T>(HashSet<T>? set) {
        if (set == null) return 0;
        var hash = 0;
        foreach (var item in set) hash ^= item?.GetHashCode() ?? 0;
        return hash;
    }
}
