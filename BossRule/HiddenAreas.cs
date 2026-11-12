using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace BossRule;

/// <summary>
/// (0.3.21) The room's hidden areas, each uncovered here while another player's body is inside it
/// (<see cref="HiddenArea"/>): a Remasker through the game's own ForceUncovered, which nothing else in the game sets (its
/// own fade runs, and it saves the area as found, as the doer's game does); an Unmasker, which uncovers for good, through
/// its own Uncover (Bone_01's Secret Mask opened on the walker's screen only). A blackout mask's cover sprites are hidden
/// here while it is open for another player and this game's own hero is outside it (<see cref="HiddenArea.HidesCover"/>).
/// </summary>
internal static class HiddenAreas {
    // a room's masks come up with it and its additive scenes: looked for again every LookEvery
    private const float LookEvery = 1.0f;

    /// <summary>The bodies of the other players in this room; set by the client add-on.</summary>
    public static Func<IEnumerable<GameObject>>? Friends;

    private static readonly AccessTools.FieldRef<Remasker, bool> BlackoutRef =
        AccessTools.FieldRefAccess<Remasker, bool>("blackoutMask");

    private static readonly AccessTools.FieldRef<Remasker, bool> InsideRef =
        AccessTools.FieldRefAccess<Remasker, bool>("isInside");

    private static readonly AccessTools.FieldRef<Remasker, SpriteRenderer[]> SpritesRef =
        AccessTools.FieldRefAccess<Remasker, SpriteRenderer[]>("childSprites");

    // the blackout covers this hid: only these are shown again (a cover the game itself turned off stays off)
    private static readonly HashSet<SpriteRenderer> Hidden = [];

    private static string? _scene;
    private static float _nextLook;
    private static Remasker[] _masks = [];
    private static Unmasker[] _unmaskers = [];

    /// <summary>Every frame: each mask uncovered for whoever else is inside it; none while not connected.</summary>
    public static void Tick(bool connected) {
        var scene = GameManager.SilentInstance?.sceneName;
        if (scene != _scene || Time.unscaledTime >= _nextLook) {
            if (scene != _scene) Hidden.RemoveWhere(sprite => !sprite);
            _scene = scene;
            _nextLook = Time.unscaledTime + LookEvery;
            _masks = UnityEngine.Object.FindObjectsByType<Remasker>(FindObjectsSortMode.None);
            _unmaskers = UnityEngine.Object.FindObjectsByType<Unmasker>(FindObjectsSortMode.None);
        }

        var bodies = connected && Friends != null
            ? Friends().Select(go => go ? go.GetComponent<BoxCollider2D>() : null).Where(c => c && c!.enabled).ToList()
            : [];
        foreach (var unmasker in _unmaskers) {
            if (unmasker && unmasker.isActiveAndEnabled && HiddenArea.OpenFor(false, Inside(unmasker, bodies))) {
                unmasker.Uncover();  // once: it does nothing when already uncovered
            }
        }

        foreach (var mask in _masks) {
            if (!mask) continue;
            var blackout = BlackoutRef(mask);
            var open = HiddenArea.OpenFor(blackout, Inside(mask, bodies));
            if (mask.ForceUncovered != open) {
                Debug.Log($"[BossRule] Hidden area {(open ? "opened" : "left")} for another player: {mask.name} ({scene})");
                mask.ForceUncovered = open;
            }
            if (blackout) Show(mask, !HiddenArea.HidesCover(true, open, InsideRef(mask)), scene);
        }
    }

    // a blackout mask's cover sprites drawn or hidden; shown again only those this hid
    private static void Show(Remasker mask, bool shown, string? scene) {
        var changed = false;
        foreach (var sprite in SpritesRef(mask) ?? []) {
            if (!sprite) continue;
            if (!shown && sprite.enabled) {
                sprite.enabled = false;
                changed = Hidden.Add(sprite) | changed;
            } else if (shown && Hidden.Remove(sprite)) {
                sprite.enabled = true;
                changed = true;
            }
        }
        if (changed) Debug.Log($"[BossRule] Blackout cover {(shown ? "shown" : "hidden")} for another player: {mask.name} ({scene})");
    }

    private static bool Inside(Component mask, List<BoxCollider2D?> bodies) => bodies.Count > 0 && mask
        .GetComponents<Collider2D>().Any(area => area.enabled && bodies.Any(body => Physics2D.Distance(area, body!).isOverlapped));
}
