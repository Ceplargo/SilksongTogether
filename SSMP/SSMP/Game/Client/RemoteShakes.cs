using UnityEngine;

namespace SSMP.Game.Client;

/// <summary>
/// (coop, 0.3.21 Task 8) The Unity side of <see cref="ShakeView"/>: whether a remote player's hit is being applied in
/// this game (GamePatcher's DamageEnemies hook), and whether a shake's source shows on this screen.
/// </summary>
internal static class RemoteShakes {
    private static int _remoteHits;

    /// <summary>Whether a remote player's hit is being applied in this game now.</summary>
    public static bool InRemoteHit => _remoteHits > 0;

    public static void Begin() => _remoteHits++;

    public static void End() => _remoteHits = Mathf.Max(0, _remoteHits - 1);

    /// <summary>
    /// Whether the source shows on this screen, as the game's own visible-only shakes judge it (a renderer's
    /// isVisible): any renderer still on, on its object or under it, else on its parent or under that; with none on (a
    /// hit that breaks or kills it turns them off before its shake: the host's screen lost the friend's in-view rosary
    /// and kill shakes, run 2026-11-09 c-shakeview-green-p2-2), whether its place is inside the main camera's view. An
    /// unknown source counts as shown (the shake as before).
    /// </summary>
    /// <summary>A left-out shake for the log: its name, its source's path, each holder's renderers on and seen, the
    /// camera's place.</summary>
    public static string Described(object? shake, Object? source) {
        var obj = source switch {
            Component c => c ? c.gameObject : null,
            GameObject g => g ? g : null,
            _ => null
        };
        var text = $"{(shake as Object)?.name ?? shake?.GetType().Name} from {(obj ? obj!.name : source?.GetType().Name)}";
        if (obj == null) return text;
        foreach (var holder in new[] { obj.transform, obj.transform.parent }) {
            if (holder == null) continue;
            int on = 0, seen = 0;
            foreach (var renderer in holder.GetComponentsInChildren<Renderer>()) {
                if (!renderer.enabled) continue;
                on++;
                if (renderer.isVisible) seen++;
            }

            text += $"; {holder.name} at {holder.position}: {on} renderers on, {seen} seen";
        }

        var cameras = GameCameras.instance;
        return text + (cameras && cameras.mainCamera ? $"; camera at {cameras.mainCamera.transform.position}" : "");
    }

    public static bool Shows(Object? source) {
        var obj = source switch {
            Component c => c ? c.gameObject : null,
            GameObject g => g ? g : null,
            _ => null
        };
        if (obj == null) return true;
        foreach (var holder in new[] { obj.transform, obj.transform.parent }) {
            if (holder == null) continue;
            var on = false;
            foreach (var renderer in holder.GetComponentsInChildren<Renderer>()) {
                if (!renderer.enabled) continue;
                if (renderer.isVisible) return true;
                on = true;
            }

            if (on) return false;
        }

        var cameras = GameCameras.instance;
        var camera = cameras ? cameras.mainCamera : null;
        if (camera == null) return true;
        var at = camera.WorldToViewportPoint(obj.transform.position);
        return at.z > 0 && at.x is >= 0 and <= 1 && at.y is >= 0 and <= 1;
    }
}
