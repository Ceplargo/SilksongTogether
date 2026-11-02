using System.Collections.Generic;
using System.Linq;
using SSMP.Api.Client;
using UnityEngine;

namespace BossRule;

/// <summary>
/// Who is in a boss fight's arena, as the scene host's game sees it: the local hero and the other players' objects.
/// </summary>
internal static class Presence {
    /// <summary>
    /// Id for the local player, whom SSMP's client API does not number; the waiting rule only uses names.
    /// </summary>
    public const ushort LocalId = ushort.MaxValue;

    /// <summary>
    /// The arena's colliders: the active loaded objects the entry's area names; (coop) while none of them is active, the
    /// switched-off ones (ArenaBounds reads their shape). Empty for a room-wide area.
    /// </summary>
    public static List<Collider2D> AreaColliders(BossEntry entry) {
        if (entry.Area.Kind != "trigger") return new List<Collider2D>();
        var named = entry.Area.Object!.Split('|').SelectMany(SceneObjects.Named).ToList();
        var active = named.Where(go => go.activeInHierarchy).ToList();
        return (active.Count > 0 || !UsesOffArena() ? active : named)
            .Select(go => go.GetComponent<Collider2D>()).Where(c => c != null).ToList();
    }

    /// <summary>
    /// (coop) Whether an arena whose objects are all switched off is still the area they name: Sister Splinter's start
    /// range is switched on only when a hero reaches her far gate, so her arena read as the whole room and her start
    /// was never held for the friend.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static bool UsesOffArena() => true;

    /// <summary>
    /// Every connected player and whether they are in the arena: inside one of its colliders, or in the room for a
    /// room-wide area (also when the arena's objects are missing, so a fight can never wait forever).
    /// </summary>
    public static IReadOnlyCollection<PlayerPresence> Collect(BossEntry entry, IClientManager client, List<Collider2D> area) {
        var anywhere = entry.Area.Kind != "trigger" || area.Count == 0;
        bool Inside(Vector3 p) => anywhere || area.Any(c => c != null && Contains(ArenaBounds(c), p));

        var hero = HeroController.instance;
        var players = new List<PlayerPresence> {
            new(LocalId, client.Username, true, hero != null && Inside(hero.transform.position))
        };
        players.AddRange(client.Players.Select(p => new PlayerPresence(p.Id, p.Username, true,
            p.IsInLocalScene && (anywhere || p.PlayerObject != null && Inside(p.PlayerObject.transform.position)))));
        return players;
    }

    /// <summary>
    /// An arena collider's world bounds from its shape, also while the game keeps it switched off, when Unity gives
    /// it empty bounds: a new save's first Moss Mother fight is started by her own FSM while her arena's trigger stays
    /// off (the game switches it on only for a re-encounter), so nobody was ever in the arena and the start was held
    /// forever.
    /// </summary>
    public static Bounds ArenaBounds(Collider2D c) {
        if (c.enabled && c.gameObject.activeInHierarchy) return c.bounds;

        IEnumerable<Vector2> shape = c switch {
            BoxCollider2D box => new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) }
                .Select(corner => box.offset + Vector2.Scale(box.size / 2, corner)),
            PolygonCollider2D polygon => Enumerable.Range(0, polygon.pathCount).SelectMany(polygon.GetPath)
                .Select(point => point + polygon.offset),
            _ => new[] { c.offset }
        };
        var world = shape.Select(point => c.transform.TransformPoint(point)).ToList();
        var bounds = new Bounds(world[0], Vector3.zero);
        foreach (var point in world) bounds.Encapsulate(point);
        return bounds;
    }

    private static bool Contains(Bounds b, Vector3 p) =>
        p.x >= b.min.x && p.x <= b.max.x && p.y >= b.min.y && p.y <= b.max.y;
}

/// <summary>
/// Finds loaded objects by a catalog name: an object's name, or the end of its path ("Parent/Child").
/// </summary>
internal static class SceneObjects {
    public static IEnumerable<GameObject> Named(string name) {
        var last = name.Substring(name.LastIndexOf('/') + 1);
        return Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(t => t.name == last && t.gameObject.scene.IsValid() && (last == name || PathIs(t, name)))
            .Select(t => t.gameObject);
    }

    /// <summary>(coop) Whether this object is the one a catalog name (its name, or the end of its path) names.</summary>
    public static bool Is(GameObject go, string name) {
        var last = name.Substring(name.LastIndexOf('/') + 1);
        return go.name == last && (last == name || PathIs(go.transform, name));
    }

    /// <summary>Whether the object's path is that path or ends with it (a path can start at the room's top object).</summary>
    private static bool PathIs(Transform t, string path) {
        var full = PathOf(t);
        return full == path || full.EndsWith("/" + path);
    }

    private static string PathOf(Transform t) => t.parent == null ? t.name : $"{PathOf(t.parent)}/{t.name}";
}
