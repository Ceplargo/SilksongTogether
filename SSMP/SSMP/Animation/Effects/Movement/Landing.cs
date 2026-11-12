using System.Runtime.CompilerServices;
using SSMP.Internals;
using UnityEngine;

namespace SSMP.Animation.Effects.Movement;

/// <summary>
/// (coop) A soft landing at a hero's copy in the other games, as the game alone lands its own hero: BackOnGround spawns
/// the hero's soft landing effect, which plays the landing's sound for the ground under this game's hero
/// (SoftLandEffect.OnEnable). The copy played its Land clip with no effect, so the other player heard no landing
/// (probe_hero_land). shortcut: hard landings are left out, as their effect (HardLandEffect) also shakes this game's
/// camera and no test save brings one about; add them once a probe can.
/// (0.3.21) The effect picks its sound for the ground under this game's hero (PlayerData.environmentType), so a copy
/// landing on other ground sounded wrong (the room walks' `extra: hornet_land_metal`): the lander sends its ground, and
/// the copy's effect is spawned with it, then this game's own is put back.
/// (0.3.21) Sent as its own clip (SoftLand) when the game spawns the lander's soft landing, not with her Land clip: a
/// hero who drops off a ledge mid-run lands with no Land clip (Bone_03's left4, probe_touch_counts), and the game also
/// replays a Land clip with no landing (Bone_07's walk, a fifth landing at the copy 40 ms after the fourth).
/// </summary>
internal class Landing : AnimationEffect {
    /// <summary>This game's hero's soft landing, once a frame (GamePatcher's two hooks on HeroController).</summary>
    internal static event System.Action? Landed;

    private static int _landedFrame = int.MinValue;

    /// <inheritdoc/>
    public override byte[]? GetEffectInfo() => LandersGround();

    /// <summary>The game spawned this game's hero's soft landing (GamePatcher's hooks on HeroController).</summary>
    internal static void SoftLanded() {
        if (GroundPick.FirstThisFrame(Time.frameCount, ref _landedFrame)) Landed?.Invoke();
    }

    /// <inheritdoc/>
    public override void Play(GameObject playerObject, CrestType crestType, byte[]? effectInfo) {
        var hero = HeroController.instance;
        if (!PlaysLandings() || hero == null || hero.softLandingEffectPrefab == null) return;
        var at = playerObject.transform.position;
        OnLandersGround(effectInfo, () => hero.softLandingEffectPrefab.Spawn(at));
    }

    /// <summary>The lander's ground, as its game reads it for its own landing effect (PlayerData.environmentType).</summary>
    internal static byte[]? LandersGround() =>
        PlayerData.instance == null ? null : [(byte) PlayerData.instance.environmentType];

    /// <summary>
    /// Spawns a copy's landing effect on the lander's ground (effectInfo: LandersGround), then puts this game's own back:
    /// the effects read the ground under this game's hero as they are enabled.
    /// </summary>
    internal static void OnLandersGround(byte[]? effectInfo, System.Action spawn) {
        var pd = PlayerData.instance;
        var own = pd.environmentType;
        if (effectInfo is { Length: > 0 }) pd.environmentType = (GlobalEnums.EnvironmentTypes) effectInfo[0];
        try {
            spawn();
        } finally {
            pd.environmentType = own;
        }
    }

    /// <summary>
    /// Not inlined: copy_land's twin skips it, which gives false.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool PlaysLandings() => true;
}
