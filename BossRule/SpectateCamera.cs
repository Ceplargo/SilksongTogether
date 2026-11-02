using HarmonyLib;
using UnityEngine;

namespace BossRule;

/// <summary>
/// Points the camera at a friend while the local player watches. The game's CameraTarget follows its private
/// <c>heroTransform</c> (set once per room in <c>SceneInit</c>), so swapping that transform keeps all of the camera's
/// own behaviour (lock zones, damping) for the friend.
/// </summary>
internal static class SpectateCamera {
    private static Transform? _own;

    public static void Follow(Transform? target) {
        var cameraTarget = GameCameras.instance != null ? GameCameras.instance.cameraTarget : null;
        if (cameraTarget == null || target == null) return;

        var followed = Traverse.Create(cameraTarget).Field("heroTransform");
        if (_own == null) _own = followed.GetValue<Transform>();
        if (followed.GetValue<Transform>() != target) followed.SetValue(target);
    }

    public static void Restore() {
        var cameraTarget = GameCameras.instance != null ? GameCameras.instance.cameraTarget : null;
        if (cameraTarget != null && _own != null) Traverse.Create(cameraTarget).Field("heroTransform").SetValue(_own);
        _own = null;
    }
}
