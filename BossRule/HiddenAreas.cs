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
    /// <summary>Adds the bodies of the other players in this room to the list; set by the client add-on.</summary>
    public static Action<List<GameObject>>? Friends;

    private static readonly AccessTools.FieldRef<Remasker, bool> BlackoutRef =
        AccessTools.FieldRefAccess<Remasker, bool>("blackoutMask");

    private static readonly AccessTools.FieldRef<Remasker, bool> InsideRef =
        AccessTools.FieldRefAccess<Remasker, bool>("isInside");

    private static readonly AccessTools.FieldRef<Remasker, SpriteRenderer[]> SpritesRef =
        AccessTools.FieldRefAccess<Remasker, SpriteRenderer[]>("childSprites");

    // the blackout covers this hid: only these are shown again (a cover the game itself turned off stays off)
    private static readonly HashSet<SpriteRenderer> Hidden = [];

    private static string? _scene;
    // the masks the game has enabled, each with its own areas, told by their OnEnable and OnDisable (optimization, fix C:
    // a look for them every second took about 50 KB each time, mini PC job 32b); an Unmasker has no OnDisable, so a
    // disabled one stays listed and is skipped, and the destroyed ones go at the next room
    private static readonly Dictionary<Remasker, Collider2D[]> Enabled = new();
    private static readonly Dictionary<Unmasker, Collider2D[]> EnabledUnmaskers = new();
    private static bool _changed;
    private static Remasker[] _masks = [];
    private static Unmasker[] _unmaskers = [];
    // the listed masks' copy that Tick walks (a mask enabled or disabled inside the walk changes only the list); and this frame's bodies (optimization, fix C: every frame built
    // them anew, 111 MB in 15 minutes of the host's game and 127 of the friend's, mini PC job 28)
    private static Collider2D[][] _maskAreas = [], _unmaskerAreas = [];
    private static readonly List<GameObject> FriendObjects = [];
    private static readonly List<BoxCollider2D> Bodies = [];

    /// <summary>Every frame: each mask uncovered for whoever else is inside it; none while not connected.</summary>
    public static void Tick(bool connected) {
        var scene = GameManager.SilentInstance?.sceneName;
        if (scene != _scene || _changed) Look(scene);

        Bodies.Clear();
        if (connected && Friends != null) {
            FriendObjects.Clear();
            Friends(FriendObjects);
            foreach (var go in FriendObjects) {
                var body = go ? go.GetComponent<BoxCollider2D>() : null;
                if (body && body!.enabled) Bodies.Add(body);
            }
        }

        for (var i = 0; i < _unmaskers.Length; i++) {
            var unmasker = _unmaskers[i];
            if (unmasker && unmasker.isActiveAndEnabled && HiddenArea.OpenFor(false, Inside(_unmaskerAreas[i]))) {
                unmasker.Uncover();  // once: it does nothing when already uncovered
            }
        }

        for (var i = 0; i < _masks.Length; i++) {
            var mask = _masks[i];
            if (!mask) continue;
            var blackout = BlackoutRef(mask);
            var open = HiddenArea.OpenFor(blackout, Inside(_maskAreas[i]));
            if (mask.ForceUncovered != open) {
                Debug.Log($"[BossRule] Hidden area {(open ? "opened" : "left")} for another player: {mask.name} ({scene})");
                mask.ForceUncovered = open;
            }
            if (blackout) Show(mask, !HiddenArea.HidesCover(true, open, InsideRef(mask)), scene);
        }
    }

    /// <summary>The game's masks tell this list when they are enabled and disabled.</summary>
    public static void Apply(Harmony harmony) {
        harmony.Patch(AccessTools.Method(typeof(Remasker), "OnEnable"),
            postfix: new HarmonyMethod(typeof(HiddenAreas), nameof(MaskEnabled)));
        harmony.Patch(AccessTools.Method(typeof(Remasker), "OnDisable"),
            postfix: new HarmonyMethod(typeof(HiddenAreas), nameof(MaskDisabled)));
        harmony.Patch(AccessTools.Method(typeof(Unmasker), "OnEnable"),
            postfix: new HarmonyMethod(typeof(HiddenAreas), nameof(UnmaskerEnabled)));
    }

    private static void MaskEnabled(Remasker __instance) {
        Enabled[__instance] = __instance.GetComponents<Collider2D>();
        _changed = true;
    }

    private static void MaskDisabled(Remasker __instance) => _changed = Enabled.Remove(__instance) | _changed;

    private static void UnmaskerEnabled(Unmasker __instance) {
        EnabledUnmaskers[__instance] = __instance.GetComponents<Collider2D>();
        _changed = true;
    }

    // the listed masks copied for Tick, after a room change or a mask enabled or disabled; its own method, so CoopQA
    // allocs reads its cost apart (job 32)
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void Look(string? scene) {
        if (scene != _scene) {
            Hidden.RemoveWhere(sprite => !sprite);
            foreach (var gone in EnabledUnmaskers.Keys.Where(unmasker => !unmasker).ToList()) EnabledUnmaskers.Remove(gone);
        }
        _scene = scene;
        _changed = false;
        _masks = [.. Enabled.Keys];
        _maskAreas = [.. Enabled.Values];
        _unmaskers = [.. EnabledUnmaskers.Keys];
        _unmaskerAreas = [.. EnabledUnmaskers.Values];
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

    // whether another player's body overlaps one of a mask's enabled areas
    private static bool Inside(Collider2D[] areas) {
        if (Bodies.Count == 0) return false;
        foreach (var area in areas) {
            if (!area || !area.enabled) continue;
            foreach (var body in Bodies) {
                if (Physics2D.Distance(area, body).isOverlapped) return true;
            }
        }
        return false;
    }
}
