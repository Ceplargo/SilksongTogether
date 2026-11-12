using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SSMP.Game.Client;

/// <summary>
/// (coop) On another player's copy (PlayerManager's player prefab): the grass it goes through reacts as it does to this
/// game's own hero, moving and playing its push sound. The game's grass (Grass, GrassBehaviour, TownGrass) reacts in its
/// OnTriggerEnter2D to whatever enters it, but the copy is a trigger on the Default layer that the grass never meets, so
/// the other player saw and heard none of the grass a hero walked through (the room walk's Bone_02). Each frame the
/// copy's box is overlapped against the scene's colliders; each grass object it newly overlaps gets its own
/// OnTriggerEnter2D with the copy's collider, and one it leaves its OnTriggerExit2D, once each, as the game's trigger
/// does (GrassContacts). Only these three grass types are called: the copy must not set off anything else that reacts
/// to entering (hazards, room changes, an NPC's range).
/// </summary>
internal class RemoteGrass : MonoBehaviour {
    /// <summary>The grass types and their own enter and exit handlers (null where a type has none).</summary>
    private static readonly (System.Type type, MethodInfo? enter, MethodInfo? exit)[] Handlers =
        [Of(typeof(Grass)), Of(typeof(GrassBehaviour)), Of(typeof(TownGrass))];

    /// <summary>Every layer, triggers too: the grass's colliders are triggers.</summary>
    private static readonly ContactFilter2D AnyCollider = new ContactFilter2D().NoFilter();

    private readonly Collider2D[] _hits = new Collider2D[32];
    private readonly HashSet<int> _inside = [];
    private readonly Dictionary<int, Collider2D> _byId = new();
    private readonly List<int> _now = [];
    private BoxCollider2D? _box;

    private static (System.Type, MethodInfo?, MethodInfo?) Of(System.Type type) {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        return (type, type.GetMethod("OnTriggerEnter2D", flags), type.GetMethod("OnTriggerExit2D", flags));
    }

    private void Awake() {
        _box = GetComponent<BoxCollider2D>();
    }

    private void OnDisable() {
        _inside.Clear();
        _byId.Clear();
    }

    private void FixedUpdate() {
        if (_box == null || !FeelsGrass()) {
            return;
        }

        var bounds = _box.bounds;
        var count = Physics2D.OverlapBox(bounds.center, bounds.size, 0f, AnyCollider, _hits);
        _now.Clear();
        for (var i = 0; i < count; i++) {
            var hit = _hits[i];
            if (hit == null || hit == _box || !IsGrass(hit)) {
                continue;
            }

            var id = hit.GetInstanceID();
            _byId[id] = hit;
            _now.Add(id);
        }

        var (entered, left) = GrassContacts.Step(_inside, _now);
        foreach (var id in entered) {
            Call(_byId[id], true);
        }

        foreach (var id in left) {
            if (_byId.TryGetValue(id, out var gone) && gone != null) {
                Call(gone, false);
            }

            _byId.Remove(id);
        }
    }

    private static bool IsGrass(Collider2D hit) {
        foreach (var (type, _, _) in Handlers) {
            if (hit.GetComponent(type) != null) return true;
        }

        return false;
    }

    private void Call(Collider2D grass, bool enter) {
        foreach (var (type, onEnter, onExit) in Handlers) {
            var handler = enter ? onEnter : onExit;
            if (handler == null) continue;
            foreach (var component in grass.GetComponents(type)) {
                if (component is Behaviour { isActiveAndEnabled: true }) {
                    handler.Invoke(component, [_box]);
                }
            }
        }
    }

    /// <summary>
    /// (coop) Whether another player's copy moves the grass it goes through. Not inlined: copy_grass' twin skips it,
    /// which gives false.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool FeelsGrass() => true;
}
