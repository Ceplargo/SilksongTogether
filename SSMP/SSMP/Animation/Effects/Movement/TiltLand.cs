using SSMP.Game.Client;
using SSMP.Internals;
using UnityEngine;

namespace SSMP.Animation.Effects.Movement;

/// <summary>
/// (coop) A hero's landing on a tilting platform, sent by her game (GamePatcher's hook on TiltPlat.OnCollisionEnter2D):
/// her copy lands there again in this game if it already stands on it (<see cref="RemoteTouches.TiltLanded"/>).
/// </summary>
internal class TiltLand : AnimationEffect {
    /// <inheritdoc/>
    public override byte[]? GetEffectInfo() => null;

    /// <inheritdoc/>
    public override void Play(GameObject playerObject, CrestType crestType, byte[]? effectInfo) {
        var touches = playerObject.GetComponent<RemoteTouches>();
        if (touches != null) touches.TiltLanded();
    }
}
