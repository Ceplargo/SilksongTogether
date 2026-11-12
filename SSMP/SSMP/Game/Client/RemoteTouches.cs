using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using Logger = SSMP.Logging.Logger;

namespace SSMP.Game.Client;

/// <summary>
/// (coop, 0.3.21) On another player's copy (PlayerManager's player prefab): what the game's own hero sets off by touch
/// shows here too, where the other player walks or lands: the bone bridges dip with dust (BoneSegment), a tilting metal
/// platform drops, tilts and creaks (TiltPlat), a dusty platform drops its dirt (DustyPlatform), loose rubble is pushed
/// with its sound (PushableRubble, Bone_12). Each reacts to a contact
/// with a hero in the game's own code (a trigger on a hero's layer; a collision from above), which the copy, a trigger
/// on the Default layer, never makes (the judges chat's probe_touch_sources). As RemoteGrass does for grass, each frame
/// the copy's box is read against the scene's colliders: a bone segment it goes into gets its own OnTriggerEnter2D with
/// a stand-in collider on layer 26, one of the layers the segment takes (kept inactive, so it meets nothing else), and
/// OnTriggerExit2D as it leaves; a tilting platform it stands on top of (TouchLanding) gets what its
/// OnCollisionEnter2D does for a hero, and what its OnCollisionExit2D does as the copy leaves it. A dusty platform
/// drops its dirt when the copy's hero's game sends that hers did (DustLand, DustSpots): by the copy's place it fell
/// where her game had none (Bone_08's walk).
/// </summary>
internal class RemoteTouches : MonoBehaviour {
    /// <summary>A layer BoneSegment takes that does not rumble the pad (9, the hero's, does).</summary>
    private const int ProxyLayer = 26;

    private static readonly ContactFilter2D AnyCollider = new ContactFilter2D().NoFilter();

    private static readonly System.Reflection.MethodInfo BoneEnter =
        AccessTools.Method(typeof(BoneSegment), "OnTriggerEnter2D");

    private static readonly System.Reflection.MethodInfo BoneExit =
        AccessTools.Method(typeof(BoneSegment), "OnTriggerExit2D");

    private static readonly System.Reflection.MethodInfo RubbleEnter =
        AccessTools.Method(typeof(PushableRubble), "OnTriggerEnter2D");

    private readonly Collider2D[] _hits = new Collider2D[32];
    /// <summary>(coop) How far below the copy's box a touch still counts (a body's contact skin).</summary>
    private const float TouchSkin = 0.05f;

    private readonly HashSet<int> _bones = [];

    /// <summary>(coop, 0.3.21) Segments the copy's box left, released once off a moment (BoneHolds).</summary>
    private readonly BoneHolds _boneHolds = new();

    private readonly Dictionary<int, BoneSegment> _held = new();
    private readonly HashSet<int> _platforms = [];
    private readonly Dictionary<int, Component> _byId = new();
    private readonly List<int> _now = [];
    private BoxCollider2D? _box;
    private BoxCollider2D? _proxy;
    private bool _full;
    private double _positionLandAt = double.MinValue;
    private bool _positionLandMatched = true;
    private double _sentLandAt = double.MinValue;
    private bool _sentLandMatched = true;

    /// <summary>(coop) This game's hero landed on a tilting platform (GamePatcher's hook): sent to her copies.</summary>
    internal static event System.Action? HeroTiltLanded;

    internal static void HeroLandedOnTilt() => HeroTiltLanded?.Invoke();

    /// <summary>(coop) This game's hero set off the dirt fall of the dusty platform at (x, y) (GamePatcher's hook).</summary>
    internal static event System.Action<float, float>? HeroDustLanded;

    internal static void HeroLandedOnDust(Vector3 at) => HeroDustLanded?.Invoke(at.x, at.y);

    /// <summary>
    /// (coop) Its hero's game sent the dirt fall of the dusty platform at (x, y) (DustLand): this game's platform
    /// there drops its dirt, as for its own hero.
    /// </summary>
    internal void DustLanded(float x, float y) {
        if (!FeelsTouches()) return;
        var plats = FindObjectsByType<DustyPlatform>(FindObjectsSortMode.None);
        var spots = new (float x, float y)[plats.Length];
        for (var i = 0; i < plats.Length; i++) {
            var place = plats[i].transform.position;
            spots[i] = (place.x, place.y);
        }

        var at = DustSpots.Nearest(spots, x, y);
        if (at >= 0) Dust(plats[at]);
    }

    /// <summary>
    /// (coop) Its hero's game sent a landing on a tilting platform (TiltRelands): the copy's own landing there, by its
    /// place, is matched with it; a landing while the copy already stands on it is its hero's lift and landing again.
    /// </summary>
    internal void TiltLanded() {
        if (!FeelsTouches() || !Relands()) return;
        var now = Time.timeAsDouble;
        TiltPlat? on = null;
        foreach (var id in _platforms) {
            if (_byId.TryGetValue(id, out var plat) && plat is TiltPlat tilt && tilt &&
                Traverse.Create(tilt).Field("heroTrans").GetValue<Transform>() == transform) on = tilt;
        }

        switch (TiltRelands.Decide(on != null, _positionLandMatched ? double.MaxValue : now - _positionLandAt)) {
            case TiltLanding.Wait:
                _sentLandAt = now;
                _sentLandMatched = false;
                return;
            case TiltLanding.Same:
                _positionLandMatched = true;
                return;
            default:
                if (on != null && on.isActiveAndEnabled) TiltDown(on, Traverse.Create(on));
                return;
        }
    }

    /// <summary>
    /// (coop) Not inlined: touch_effects' run over Bone_03's plate fails with it skipped (gives false): the copy lands
    /// only by its place.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool Relands() => true;

    private void Awake() {
        _box = GetComponent<BoxCollider2D>();
        var stand = new GameObject("Touch Proxy") { layer = ProxyLayer };
        stand.SetActive(false);
        stand.transform.SetParent(transform, false);
        _proxy = stand.AddComponent<BoxCollider2D>();
    }

    private void OnDisable() {
        foreach (var id in _platforms) {
            if (_byId.TryGetValue(id, out var plat) && plat is TiltPlat tilt && tilt) Leave(tilt);
        }

        foreach (var id in _bones) {
            if (_byId.TryGetValue(id, out var bone) && bone is BoneSegment segment && segment) Bone(segment, false);
        }

        foreach (var id in _boneHolds.All()) {  // left a moment ago, waiting: released with the copy gone
            if (_held.Remove(id, out var held) && held) Bone(held, false);
        }

        _held.Clear();

        _bones.Clear();
        _platforms.Clear();
        _byId.Clear();
    }

    private void FixedUpdate() {
        if (_box == null || _proxy == null || !FeelsTouches()) {
            return;
        }

        var bounds = _box.bounds;
        // (coop, 0.3.21) a skin below the feet, as a body's contact keeps one: placed at the hero's own height, the box's
        // bottom sat on a bone segment's trigger edge and lost it a moment (probe_bridge_walk 235122-pair2: Segment 6
        // released and pressed again by the copy alone)
        var skin = HoldsBones() ? TouchSkin : 0f;
        var count = Physics2D.OverlapBox(bounds.center - new Vector3(0f, skin * 0.5f), bounds.size + new Vector3(0f, skin),
            0f, AnyCollider, _hits);
        if (count == _hits.Length && !_full) {
            _full = true;  // print only, once a copy: more colliders under it than the buffer holds
            Logger.Info($"RemoteTouches: {name}'s box meets {count} or more colliders at {bounds.center}");
        }
        Step(_bones, count, hit => {
            // the rubble's own collider is solid: a hero's trigger box (HeroBox) sets it off
            Component? thing = hit.GetComponentInParent<PushableRubble>();
            if (thing == null && hit.isTrigger) thing = hit.GetComponent<BoneSegment>();
            return thing;
        }, c => {
            if (c is BoneSegment segment) {
                // back on it before its hold ran out: still pressed, no new press (BoneHolds)
                if (HoldsBones() && _boneHolds.Back(segment.GetInstanceID())) _held.Remove(segment.GetInstanceID());
                else Bone(segment, true);
            } else if (c is PushableRubble rubble && rubble.isActiveAndEnabled) {
                // print only (0.3.21: the rubble's push at the copy came and went between runs)
                Logger.Info($"RemoteTouches: {name} pushes {rubble.name} at {rubble.transform.position}");
                RubbleEnter.Invoke(rubble, [_proxy]);
            }
        }, c => {
            if (c is not BoneSegment segment) return;
            if (!HoldsBones()) {
                Bone(segment, false);
                return;
            }

            _held[segment.GetInstanceID()] = segment;
            _boneHolds.Left(segment.GetInstanceID(), Time.timeAsDouble);
        });
        foreach (var id in _boneHolds.Due(Time.timeAsDouble)) {
            if (_held.Remove(id, out var segment) && segment) Bone(segment, false);
        }

        var feet = new Vector2(bounds.center.x, bounds.min.y);
        count = Physics2D.OverlapBox(feet, new Vector2(bounds.size.x, TouchLanding.Tolerance * 2f), 0f, AnyCollider,
            _hits);
        Step(_platforms, count, hit => {
            if (hit.isTrigger) return null;
            Component? plat = hit.GetComponent<TiltPlat>();
            var top = hit.bounds;
            return plat != null &&
                   TouchLanding.OnTop(bounds.min.y, bounds.min.x, bounds.max.x, top.max.y, top.min.x, top.max.x)
                ? plat
                : null;
        }, Land, c => {
            if (c is TiltPlat tilt) Leave(tilt);
        });
    }

    /// <summary>The ones of the count hits that pick takes, entered and left against inside, each once.</summary>
    private void Step(
        HashSet<int> inside,
        int count,
        System.Func<Collider2D, Component?> pick,
        System.Action<Component> enter,
        System.Action<Component> leave
    ) {
        _now.Clear();
        for (var i = 0; i < count; i++) {
            var hit = _hits[i];
            if (hit == null || hit == _box) continue;
            var thing = pick(hit);
            if (thing == null) continue;
            var id = thing.GetInstanceID();
            _byId[id] = thing;
            if (!_now.Contains(id)) _now.Add(id);
        }

        var (entered, left) = GrassContacts.Step(inside, _now);
        foreach (var id in entered) enter(_byId[id]);
        foreach (var id in left) {
            if (_byId.TryGetValue(id, out var gone) && gone) leave(gone);
            _byId.Remove(id);
        }
    }

    /// <summary>(coop) Whether a left segment's release waits (BoneHolds). Not inlined: its twin skips it.</summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static bool HoldsBones() => true;

    private void Bone(BoneSegment segment, bool enter) {
        if (segment.isActiveAndEnabled) (enter ? BoneEnter : BoneExit).Invoke(segment, [_proxy]);
    }

    /// <summary>What a platform's OnCollisionEnter2D does for a hero landing on it.</summary>
    private void Land(Component plat) {
        if (plat is not TiltPlat tilt || !tilt.isActiveAndEnabled) return;
        var t = Traverse.Create(tilt);
        // a hero already on it tilts it after itself, as the one hero does in the game alone
        if (t.Field("heroTrans").GetValue<Transform>()) return;
        TiltDown(tilt, t);
        // a landing its hero's game sent just before (TiltLanded) is this one; else one may follow
        var now = Time.timeAsDouble;
        if (!_sentLandMatched && now - _sentLandAt <= TiltRelands.SameS) {
            _sentLandMatched = true;
        } else {
            _positionLandAt = now;
            _positionLandMatched = false;
        }
    }

    /// <summary>What TiltPlat.OnCollisionEnter2D does for a hero landing on it, the copy as its hero.</summary>
    private void TiltDown(TiltPlat tilt, Traverse t) {
        t.Field("heroTrans").SetValue(transform);
        t.Method("StopMoveRoutines").GetValue();
        t.Field("dropRoutine").SetValue(tilt.StartCoroutine(t.Method("ArtDrop").GetValue<IEnumerator>()));
        t.Field("OnLand").GetValue<UnityEvent>()?.Invoke();
        t.Field("landAudioClipTable").GetValue<RandomAudioClipTable>()?.SpawnAndPlayOneShot(tilt.transform.position);
        if (t.Field("tiltRoutine").GetValue<Coroutine>() == null) {
            t.Field("tiltRoutine").SetValue(tilt.StartCoroutine(t.Method("ArtTilt").GetValue<IEnumerator>()));
        }
    }

    /// <summary>What a tilting platform's OnCollisionExit2D does as the hero on it leaves.</summary>
    private void Leave(TiltPlat tilt) {
        var t = Traverse.Create(tilt);
        if (t.Field("heroTrans").GetValue<Transform>() != transform) return;
        t.Field("heroTrans").SetValue(null);
        if (!tilt.isActiveAndEnabled) return;
        t.Method("StopMoveRoutines").GetValue();
        t.Field("dropRoutine").SetValue(tilt.StartCoroutine(t.Method("ArtRaise").GetValue<IEnumerator>()));
    }

    /// <summary>DustyPlatform.OnCollisionEnter2D's dust, sound and stream for a landing from above.</summary>
    private static void Dust(DustyPlatform plat) {
        var t = Traverse.Create(plat);
        if (t.Field("isRunning").GetValue<bool>()) return;
        var position = plat.transform.position;
        t.Field("dustFallClips").GetValue<RandomAudioClipTable>()
            ?.SpawnAndPlayOneShot(t.Field("dustFallSourcePrefab").GetValue<AudioSource>(), position);
        var body = t.Field("bodyCollider").GetValue<BoxCollider2D>();
        var inset = t.Field("inset").GetValue<float>();
        var area = body.size - new Vector2(inset, inset);
        var at = position;
        at.z = -0.1f;
        var dust = t.Field("dustPrefab").GetValue<ParticleSystem>();
        if (dust != null) {
            var ps = dust.Spawn(at);
            var emission = ps.emission;
            emission.rateOverTime = area.x * area.y * t.Field("dustRateAreaFactor").GetValue<float>() +
                                    t.Field("dustRateConstant").GetValue<float>();
            ps.transform.localScale = new Vector3(area.x, area.y, ps.transform.localScale.z);
        }

        var stream = t.Field("streamPrefab").GetValue<GameObject>();
        if (stream != null) {
            var spawned = stream.Spawn(at + new Vector3(0f, -body.size.y * 0.5f, 0.01f) +
                                       t.Field("streamOffset").GetValue<Vector3>());
            var scale = spawned.transform.localScale;
            scale.x = area.x;
            spawned.transform.localScale = scale;
        }

        var rocks = t.Field("rocksPrefab").GetValue<ParticleSystem>();
        if (Random.value < t.Field("rocksChance").GetValue<float>() && rocks != null) {
            var spawned = rocks.Spawn(at).transform;
            spawned.position = new Vector3(spawned.position.x, spawned.position.y, 0.003f);
            spawned.localScale = new Vector3(area.x, area.y, spawned.localScale.z);
        }

        t.Field("cooldownTimer").SetValue(t.Field("cooldownDuration").GetValue<float>());
        t.Field("isRunning").SetValue(true);
    }

    /// <summary>
    /// (coop) Whether another player's copy sets off what its touch sets off. Not inlined: touch_effects' twin skips
    /// it, which gives false.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool FeelsTouches() => true;
}
