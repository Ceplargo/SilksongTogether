using System.Runtime.CompilerServices;
using SSMP.Internals;
using UnityEngine;

namespace SSMP.Animation.Effects.Movement;

/// <summary>
/// (coop) A hard landing at a hero's copy in the other games, as the game alone lands its own hero
/// (HeroController.DoHardLandingEffectNoHit): the hero's hard landing sound, and its hard landing effect, whose dust
/// is for the ground under this game's hero (HardLandEffect.OnEnable). Without the camera shake that effect sends: the
/// game alone shakes only for its own hero (the project's ruling, 2026-11-08), and without its noise, which
/// would wake this game's enemies at the copy (left as it was: a change to the
/// host's enemies needs the project). The copy played its HardLand clip with neither, so the other player's hard landing was silent and dustless.
/// (0.3.21) Its effect is spawned on the lander's ground (Landing.OnLandersGround): it turned on the dust and sound for
/// the ground under this game's hero (the room walks' g sounds in one game only).
/// </summary>
internal class HardLanding : AnimationEffect {
    /// <summary>Whether a copy's hard landing effect is being spawned now: its OnEnable then sends no shake.</summary>
    internal static bool Spawning;

    /// <inheritdoc/>
    public override byte[]? GetEffectInfo() => Landing.LandersGround();

    /// <inheritdoc/>
    public override void Play(GameObject playerObject, CrestType crestType, byte[]? effectInfo) {
        var hero = HeroController.instance;
        if (!PlaysHardLandings() || hero == null) return;
        var at = playerObject.transform.position;
        var sound = hero.AudioCtrl != null ? hero.AudioCtrl.hardLanding : null;
        if (sound != null && sound.clip != null) {
            new AudioEvent { Clip = sound.clip, PitchMin = sound.pitch, PitchMax = sound.pitch, Volume = sound.volume }
                .SpawnAndPlayOneShot(at);
        }

        var prefab = hero.hardLandingEffectPrefab;
        if (prefab == null) return;
        // A fresh copy, not the hero's pool: its NoiseMaker (which wakes this game's enemies) comes off before its
        // Start makes the first noise, and the effect's own Recycle then destroys it (ObjectPool.Recycle, unpooled).
        // Instantiated on the lander's ground: the prefab is active, so its OnEnable (which picks the dust by the
        // ground) runs in Instantiate (copy_hard_land's dust compare, 2026-11-09: the copy showed this game's ground)
        Spawning = true;
        try {
            GameObject? effect = null;
            Landing.OnLandersGround(effectInfo, () => {
                effect = UnityEngine.Object.Instantiate(prefab, at, prefab.transform.rotation);
                effect.SetActive(true);
            });
            var noise = effect != null ? effect.GetComponent<NoiseMaker>() : null;
            if (noise != null) UnityEngine.Object.DestroyImmediate(noise);
        } finally {
            Spawning = false;
        }
    }

    /// <summary>HardLandEffect.OnEnable's camera shake (GamePatcher), left out for a copy's landing.</summary>
    internal static void Shake(PlayMakerFSM shake, string name) {
        if (!Spawning || !LeavesOutShakes()) shake.SendEvent(name);
    }

    /// <summary>Not inlined: copy_hard_land's shake red (probe_hard_land_shake_twin) skips it, which gives false.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool LeavesOutShakes() => true;

    /// <summary>Not inlined: copy_hard_land's twin skips it, which gives false.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool PlaysHardLandings() => true;
}
