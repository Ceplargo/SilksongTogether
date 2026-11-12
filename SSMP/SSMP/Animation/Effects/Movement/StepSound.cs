using SSMP.Game.Client;
using SSMP.Internals;
using UnityEngine;

namespace SSMP.Animation.Effects.Movement;

/// <summary>
/// (coop) A hero's run start or footstep at its copy in the other games (<see cref="RemoteSteps"/>): her game sends one
/// whenever it plays its own hero's (EventHooks.HeroRunStart, HeroFootstep).
/// </summary>
internal class StepSound : AnimationEffect {
    /// <summary>The run start; else a footstep.</summary>
    public bool RunStart { get; init; }

    /// <inheritdoc/>
    public override byte[]? GetEffectInfo() => null;

    /// <inheritdoc/>
    public override void Play(GameObject playerObject, CrestType crestType, byte[]? effectInfo) =>
        RemoteSteps.Play(playerObject, RunStart, effectInfo);
}
