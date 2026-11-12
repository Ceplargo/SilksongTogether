using SSMP.Game.Client;
using SSMP.Internals;
using UnityEngine;

namespace SSMP.Animation.Effects.Movement;

/// <summary>
/// (coop, 0.3.21) A hero's landing on a dusty platform, sent by her game (GamePatcher's hook on
/// DustyPlatform.OnCollisionEnter2D) with the platform's place: its dirt falls in this game too
/// (<see cref="RemoteTouches.DustLanded"/>).
/// </summary>
internal class DustLand : AnimationEffect {
    /// <inheritdoc/>
    public override byte[]? GetEffectInfo() => null;

    /// <inheritdoc/>
    public override void Play(GameObject playerObject, CrestType crestType, byte[]? effectInfo) {
        var at = DustSpots.Read(effectInfo);
        var touches = playerObject.GetComponent<RemoteTouches>();
        if (at != null && touches != null) touches.DustLanded(at.Value.x, at.Value.y);
    }
}
