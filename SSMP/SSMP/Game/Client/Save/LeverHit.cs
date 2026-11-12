using System.Collections;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SSMP.Game.Client.Save;

/// <summary>
/// Another player's lever hit, played in this game as the game's own Lever_tk2d.Hit plays it, without the hitter's
/// own freeze and vibration: the lever goes on and its collider off, its hit sound, effect and camera shake, and its
/// Execute (the hit animation, then after its delay its gates' OPEN: Bone_01's Great Bone Gate runs its 10 s sequence).
/// </summary>
internal static class LeverHit {
    private static readonly FieldInfo Activated = AccessTools.Field(typeof(Lever_tk2d), "activated");
    private static readonly FieldInfo ActiveCollider = AccessTools.Field(typeof(Lever_tk2d), "activeCollider");
    private static readonly FieldInfo HitSound = AccessTools.Field(typeof(Lever_tk2d), "hitSound");
    private static readonly FieldInfo HitSoundOnActivate = AccessTools.Field(typeof(Lever_tk2d), "hitSoundOnActivate");
    private static readonly FieldInfo HitEffect = AccessTools.Field(typeof(Lever_tk2d), "hitEffect");
    private static readonly FieldInfo Effects = AccessTools.Field(typeof(Lever_tk2d), "effects");
    private static readonly MethodInfo Execute = AccessTools.Method(typeof(Lever_tk2d), "Execute");
    private static readonly FieldInfo FsmGates = AccessTools.Field(typeof(Lever_tk2d), "fsmGates");
    private static readonly FieldInfo ConnectedGates = AccessTools.Field(typeof(Lever_tk2d), "connectedGates");

    /// <summary>The room's lever that opens gate (its FSM or its unlockable prop), or null.</summary>
    public static Lever_tk2d? LeverOf(GameObject gate) {
        foreach (var lever in Object.FindObjectsByType<Lever_tk2d>(FindObjectsSortMode.None)) {
            if (lever.gameObject.scene != gate.scene) continue;
            if (FsmGates.GetValue(lever) is PlayMakerFSM[] fsms && System.Array.Exists(fsms, f => f && f.gameObject == gate) ||
                ConnectedGates.GetValue(lever) is UnlockablePropBase[] props &&
                System.Array.Exists(props, p => p && p.gameObject == gate)) {
                return lever;
            }
        }

        return null;
    }

    public static bool IsActivated(Lever_tk2d lever) => (bool) Activated.GetValue(lever);

    public static void Play(Lever_tk2d lever) {
        Activated.SetValue(lever, true);
        var collider = ActiveCollider.GetValue(lever) as Collider2D;
        if (collider) {
            collider!.enabled = false;
        }

        lever.StartCoroutine((IEnumerator) Execute.Invoke(lever, null));
        if ((bool) HitSoundOnActivate.GetValue(lever)) {
            lever.PlaySoundOneShot(HitSound.GetValue(lever) as AudioClip);
        }

        if (HitEffect.GetValue(lever) is GameObject hitEffect && hitEffect && collider) {
            hitEffect.Spawn(collider!.bounds.center);
        }

        if (Effects.GetValue(lever) is GameObject effects && effects) {
            effects.SetActive(true);
        }

        lever.PlaySound();
        var cameras = GameCameras.instance;
        if (cameras && ShakeView.Shakes(true, RemoteShakes.Shows(lever), ShakeView.OnlyInView())) {
            cameras.cameraShakeFSM.SendEvent("EnemyKillShake");
        }
    }

    // (0.3.21 Task 8) the other lever class, Lever (Bone_08's): its own hit played from the synced value. The copy of
    // the other player's swing never plays it here (its canHitTrigger counts this game's hero only), and loading the
    // value set it on from start: no hit, no sound, no shake, its platforms and gates jumped open.
    private static readonly FieldInfo PlainActivated = AccessTools.Field(typeof(Lever), "activated");
    private static readonly FieldInfo PlainAnimator = AccessTools.Field(typeof(Lever), "animator");
    private static readonly FieldInfo PlainRetract = AccessTools.Field(typeof(Lever), "retractAfterHit");
    private static readonly FieldInfo PlainHitEffect = AccessTools.Field(typeof(Lever), "hitEffect");
    private static readonly FieldInfo PlainHitEffectPoint = AccessTools.Field(typeof(Lever), "hitEffectPoint");
    private static readonly FieldInfo PlainShake = AccessTools.Field(typeof(Lever), "hitCameraShake");
    private static readonly FieldInfo PlainHitSound = AccessTools.Field(typeof(Lever), "hitSound");
    private static readonly FieldInfo PlainUnlockables = AccessTools.Field(typeof(Lever), "unlockables");
    private static readonly FieldInfo PlainFsmGates = AccessTools.Field(typeof(Lever), "fsmGates");
    private static readonly MethodInfo PlainExecute = AccessTools.Method(typeof(Lever), "Execute");
    private static readonly int HitAnim = Animator.StringToHash("Hit");
    private static readonly int RetractedAnim = Animator.StringToHash("Retracted");

    /// <summary>The room's Lever that opens gate (its FSM or its unlockable prop), or null.</summary>
    public static Lever? PlainLeverOf(GameObject gate) {
        foreach (var lever in Object.FindObjectsByType<Lever>(FindObjectsSortMode.None)) {
            if (lever.gameObject.scene != gate.scene) continue;
            if (PlainFsmGates.GetValue(lever) is PlayMakerFSM[] fsms && System.Array.Exists(fsms, f => f && f.gameObject == gate) ||
                PlainUnlockables.GetValue(lever) is UnlockablePropBase[] props &&
                System.Array.Exists(props, p => p && p.gameObject == gate)) {
                return lever;
            }
        }

        return null;
    }

    public static bool IsActivated(Lever lever) => (bool) PlainActivated.GetValue(lever);

    /// <summary>
    /// Lever.Hit as the game plays it, without the hitter's own part (its recoil): its OnHit, the lever goes on and
    /// blocks hits, its Hit animation, its Execute (after its delay its platforms' and gates' opening), its hit effect,
    /// shake (only while it shows here) and sound. shortcut: no Hit Direction for its animation (the hitter's facing is
    /// not here), its animator's default; set it when a lever's swing shows the wrong way.
    /// </summary>
    public static void Play(Lever lever) {
        lever.OnHit.Invoke();
        lever.SetActivatedInert(true);
        if (PlainAnimator.GetValue(lever) is Animator animator && animator) {
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (animator.HasParameter(HitAnim)) animator.SetTrigger(HitAnim);
            if ((bool) PlainRetract.GetValue(lever) && animator.HasParameter(RetractedAnim)) animator.SetBool(RetractedAnim, true);
        }

        lever.StartCoroutine((IEnumerator) PlainExecute.Invoke(lever, null));
        var at = PlainHitEffectPoint.GetValue(lever) is Transform point && point ? point.position : lever.transform.position;
        if (PlainHitEffect.GetValue(lever) is GameObject hitEffect && hitEffect) {
            hitEffect.Spawn(at);
        }

        if (PlainShake.GetValue(lever) is CameraShakeTarget shake &&
            ShakeView.Shakes(true, RemoteShakes.Shows(lever), ShakeView.OnlyInView())) {
            shake.DoShake(lever);
        }

        ((AudioEvent) PlainHitSound.GetValue(lever)).SpawnAndPlayOneShot(lever.transform.position);
    }
}
