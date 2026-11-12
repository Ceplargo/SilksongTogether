using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity;

/// <summary>
/// (coop, 0.3.21) A copy plays a state's in-state particles (PlayParticleEmitterInState) when the scene host's action
/// arrives and stops them at the next state change. A copy made after the scene host's enemy entered that state never
/// got the play: the Bone Goomba Large walking in Bone_03 and Bone_16 kept its walk dust (Pt Walk) in the scene host's
/// game only, as nobody came near to change its state (probe_goomba_dust). A copy that learns the scene host's state
/// starts that state's in-state particles that are not playing. Unity-free, so it is tested without the game.
/// </summary>
internal static class LateEffects {
    /// <summary>Whether a copy starts a state's in-state particle on learning the state: it has one and it is not playing.</summary>
    public static bool Needed(bool found, bool playing) => found && !playing;

    /// <summary>Whether a copy starts them. Not inlined: the check's twin skips it.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool Plays() => true;
}
