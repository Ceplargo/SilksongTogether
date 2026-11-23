using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;

namespace BossRule;

/// <summary>
/// (0.4 world Task 16) The game side of <see cref="OwnClocks"/>: this room's own-clock hazards (Greymoor's windmill
/// cogs, Dock_16's lava waterfall sets) follow the scene host's game. A game that finds them in its room, or that just
/// froze, tells the room: the scene host's game sends its phases, another game asks for them; a game that is not the
/// scene host takes each phase it gets when its own is apart (an Animator's state and place in its cycle; a loop's
/// state and its Wait's time in it).
/// </summary>
internal static class ClockFollow {
    public const string Prefix = OwnClocks.Prefix;
    // a room's objects come up over its first moments, as TrapFollow looks for its terrain
    private const float LookEvery = 0.25f;
    private const float LookFor = 3.0f;
    private const float TellAgainS = 0.5f;

    /// <summary>Sends a room word to the room's other players; set by the client add-on.</summary>
    public static Action<string, string>? Send;

    /// <summary>Whether this game is its room's scene host; set by the client add-on.</summary>
    public static Func<bool>? SceneHost;

    /// <summary>Whether this game is in a co-op session; set by the client add-on.</summary>
    public static Func<bool>? Connected;

    private static string? _scene;
    private static float _sceneAt;
    private static float _nextLook;
    private static readonly Dictionary<string, Animator> Cogs = new();
    private static readonly Dictionary<string, PlayMakerFSM> Loops = new();
    private static readonly Dictionary<string, (Animator Animator, DeactivateAfterDelay Off)> Bursts = new();
    private static float _askedAt = float.NegativeInfinity;
    private static float _leadFor = float.NegativeInfinity;
    private static float _lead;
    // the scene host's clock minus this game's, from this room's quickest ask and pong; a word's age by it
    private static float _offset;
    private static float _offsetRoundTrip = float.PositiveInfinity;
    private static bool _answered;
    private static bool _tellNext;
    private static float _tellAgainAt = float.PositiveInfinity;
    private static readonly Dictionary<string, int> OnSince = new();  // a burst on, and the frame it was first seen on

    /// <summary>Every frame: this room's own-clock hazards, and whether this game's clock just jumped.</summary>
    public static void Tick() {
        var scene = GameManager.SilentInstance?.sceneName;
        if (scene != _scene) {
            _scene = scene;
            _sceneAt = Time.unscaledTime;
            _nextLook = 0f;
            Cogs.Clear();
            Loops.Clear();
            Bursts.Clear();
            OnSince.Clear();
            _answered = false;
            _tellNext = false;
            _tellAgainAt = float.PositiveInfinity;
            _offsetRoundTrip = float.PositiveInfinity;  // another room can have another scene host
            return;  // the load's own long frame is no freeze
        }

        if (scene == null) return;
        if (Time.unscaledTime >= _nextLook && Time.unscaledTime <= _sceneAt + LookFor) {
            _nextLook = Time.unscaledTime + LookEvery;
            // a game that just came in asks, whatever it counts itself: one that came in second could still count
            // itself the scene host and sent its own phases (the first green run)
            if (Look() > 0) Ask(scene);
            return;
        }

        if (Cogs.Count + Loops.Count + Bursts.Count > 0
            && OwnClocks.AsksAgain(_answered, Time.unscaledTime - _sceneAt, Time.unscaledTime - _askedAt)) Ask(scene);

        if (Bursts.Count > 0 && SceneHost?.Invoke() == true) SendNewBursts(scene);

        if (_tellNext || Time.unscaledTime >= _tellAgainAt) {
            if (!_tellNext) _tellAgainAt = float.PositiveInfinity;
            _tellNext = false;
            Tell(scene);
        }

        // told on the next frame: this frame's own long step lands on the hazards after this Tick
        if (Cogs.Count + Loops.Count + Bursts.Count > 0 && OwnClocks.Froze(Time.unscaledDeltaTime)) {
            Debug.Log($"[BossRule] Own clocks after a {Time.unscaledDeltaTime:0.00} s frame: told again ({scene})");
            _tellNext = true;
            // and again once every hazard has stepped: one that came on inside the long frame still read as just on
            // (run 5: a lava hazard 0.36 s apart after the scene host's freeze)
            _tellAgainAt = Time.unscaledTime + TellAgainS;
        }
    }

    /// <summary>The scene host's game sends its phases; another game asks for them.</summary>
    private static void Tell(string scene) {
        if (SceneHost?.Invoke() == true) SendPhases(scene);
        else Ask(scene);
    }

    private static void Ask(string scene) {
        _askedAt = Time.unscaledTime;
        Say(scene, OwnClocks.Ask);
    }

    /// <summary>A word to the room, with this game's frame time in front (OwnClocks.Stamp).</summary>
    private static void Say(string scene, string word) => Send?.Invoke(scene, OwnClocks.Prefix + OwnClocks.Stamp(Time.unscaledTime, word));

    private static void SendPhases(string scene) {
        foreach (var (path, animator) in Cogs) {
            if (animator == null || !animator.isActiveAndEnabled) continue;
            var info = animator.GetCurrentAnimatorStateInfo(0);
            Say(scene, OwnClocks.Word(path, info.fullPathHash.ToString(), info.normalizedTime));
        }

        foreach (var (path, fsm) in Loops) {
            if (fsm == null || !fsm.enabled || fsm.ActiveStateName is not { } state) continue;
            Say(scene, OwnClocks.Word(path, state, WaitTime(fsm) ?? 0f));
        }

        // after the loops: a loop's state taken can turn one on, and its burst word then sets its time
        foreach (var (path, (animator, off)) in Bursts) {
            if (animator == null || off == null) continue;
            if (!animator.isActiveAndEnabled) {
                Say(scene, OwnClocks.Word(path, OwnClocks.Off, 0f));
                continue;
            }

            if (JustOn(path)) continue;  // SendNewBursts sends it next frame
            Say(scene, BurstWord(path, animator, off));
        }
    }

    private static string BurstWord(string path, Animator animator, DeactivateAfterDelay off) {
        var info = animator.GetCurrentAnimatorStateInfo(0);
        var scale = animator.transform.localScale;
        return OwnClocks.Word(path, OwnClocks.BurstState(true, info.fullPathHash, OffTimer(off), OwnClocks.FlipOf(scale.x, scale.y), info.length),
                              info.normalizedTime);
    }

    /// <summary>The scene host's game: each burst that came on, with its own random flip, sent the frame after it was
    /// first seen on (by then its Animator and its off-timer have both stepped; on the frame it comes on, its Animator
    /// can still read its last fall's time).</summary>
    private static void SendNewBursts(string scene) {
        foreach (var (path, (animator, off)) in Bursts) {
            if (animator == null || off == null) continue;
            if (!animator.isActiveAndEnabled) {
                OnSince.Remove(path);
                continue;
            }

            if (!OnSince.TryGetValue(path, out var since)) OnSince[path] = Time.frameCount;
            else if (since >= 0 && Time.frameCount > since) {
                OnSince[path] = -1;  // sent
                Say(scene, BurstWord(path, animator, off));
            }
        }
    }

    private static bool JustOn(string path) => OnSince.TryGetValue(path, out var since) && since == Time.frameCount;

    /// <summary>A game that is not its room's scene host: a followed burst takes the scene host's flip, not its own
    /// random one (RandomlyFlipScale.ApplyScale prefix).</summary>
    public static bool FlipPrefix(RandomlyFlipScale __instance) =>
        Connected?.Invoke() != true || SceneHost?.Invoke() == true || OwnClocks.KindOf(__instance.gameObject.name, "") != OwnClocks.Kind.Burst;

    /// <summary>Another game's word in this room: an ask (the scene host's game answers) or a phase (a game that is
    /// not the scene host takes it when its own is apart).</summary>
    public static void Echo(string text) {
        var scene = GameManager.SilentInstance?.sceneName;
        if (scene == null || !OwnClocks.TryStamp(text, out var sentAt, out var word)) return;
        var host = SceneHost?.Invoke() == true;
        if (word == OwnClocks.Ask) {
            if (!host) return;
            if (!float.IsNaN(sentAt)) Say(scene, OwnClocks.Pong + sentAt.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            SendPhases(scene);
            return;
        }

        if (word.StartsWith(OwnClocks.Pong)) {
            if (!host && !float.IsNaN(sentAt) && float.TryParse(word.Substring(OwnClocks.Pong.Length), System.Globalization.NumberStyles.Float,
                                                              System.Globalization.CultureInfo.InvariantCulture, out var askedAt)
                && Time.unscaledTime - askedAt is var roundTrip and >= 0f && roundTrip < _offsetRoundTrip) {
                _offsetRoundTrip = roundTrip;
                _offset = OwnClocks.Offset(askedAt, Time.unscaledTime, sentAt);
            }

            return;
        }

        if (!OwnClocks.Takes(host, Time.unscaledTime - _askedAt) || !OwnClocks.TryParse(word, out var path, out var state, out var value)) return;
        _answered = true;
        if (_leadFor != _askedAt && Time.unscaledTime - _askedAt < 2f) {
            _leadFor = _askedAt;  // the first answer to this ask: its round trip
            _lead = OwnClocks.Lead(Time.unscaledTime - _askedAt);
        }

        // the word's own age when its send time and the clock offset are known: a word the network held or re-sent
        // lands older than half an ask's round trip (own_clock under 50 ms lag: 0.256 s against a 0.052 s lead)
        var lead = !float.IsNaN(sentAt) && _offsetRoundTrip < float.PositiveInfinity ? OwnClocks.Age(Time.unscaledTime, sentAt, _offset) : _lead;

        if (Bursts.TryGetValue(path, out var burst) && burst.Animator != null && burst.Off != null) {
            TakeBurst(path, burst.Animator, burst.Off, state, value, lead, scene);
            return;
        }

        if (Cogs.TryGetValue(path, out var animator) && animator != null && animator.isActiveAndEnabled) {
            var info = animator.GetCurrentAnimatorStateInfo(0);
            value = OwnClocks.Ahead(OwnClocks.Kind.Animator, state, value, lead, info.length).Value;
            if (!OwnClocks.Apart(OwnClocks.Kind.Animator, info.fullPathHash.ToString(), info.normalizedTime, state, value)) return;
            animator.Play(int.Parse(state), 0, value - Mathf.Floor(value));
            Debug.Log($"[BossRule] Own clock taken: {path} at {value - Mathf.Floor(value):0.000} of its cycle ({scene})");
            return;
        }

        if (!Loops.TryGetValue(path, out var fsm) || fsm == null || !fsm.enabled) return;
        value = OwnClocks.Ahead(OwnClocks.Kind.Loop, state, value, lead, 0f).Value;
        if (!OwnClocks.LoopApart(fsm.ActiveStateName ?? "", WaitTime(fsm), state, value)) return;
        if (fsm.ActiveStateName != state) fsm.SetState(state);
        SetWaitTime(fsm, value);
        Debug.Log($"[BossRule] Own clock taken: {path} {state} at {value:0.00} s ({scene})");
    }

    /// <summary>A burst to the scene host's: on or off, and when on its Animator's place and its off-timer.</summary>
    private static void TakeBurst(string path, Animator animator, DeactivateAfterDelay off, string state, float value, float lead, string scene) {
        var on = animator.isActiveAndEnabled;
        var mineAt = 0f;
        var mine = on && OwnClocks.TryParse(BurstWord(path, animator, off), out _, out var own, out mineAt) ? own : OwnClocks.Off;
        var sent = value;
        (state, value) = OwnClocks.Ahead(OwnClocks.Kind.Burst, state, value, lead, 0f);
        if (!OwnClocks.Apart(OwnClocks.Kind.Burst, mine, mineAt, state, value)) return;
        if (state == OwnClocks.Off) {
            animator.gameObject.SetActive(false);
            Debug.Log($"[BossRule] Own clock taken: {path} off ({scene})");
            return;
        }

        if (!OwnClocks.TryBurst(state, out var hash, out var left, out var flip, out var length)) return;
        if (!on) animator.gameObject.SetActive(true);  // its OnEnable sets the off-timer to its whole time
        var scale = animator.transform.localScale;
        animator.transform.localScale = new Vector3(Mathf.Abs(scale.x) * ((flip & 1) != 0 ? -1f : 1f),
                                                    Mathf.Abs(scale.y) * ((flip & 2) != 0 ? -1f : 1f), scale.z);
        animator.Play(hash, 0, value);
        Traverse.Create(off).Field("timer").SetValue(left);
        Debug.Log($"[BossRule] Own clock taken: {path} on, {sent:0.000} of its fall as sent, {left:0.00} s left, flip {flip}, lead {lead:0.000} s ({scene})");
    }

    private static float OffTimer(DeactivateAfterDelay off) => Traverse.Create(off).Field("timer").GetValue<float>();

    /// <summary>The time the loop's active state has waited so far (its Wait action's own timer).</summary>
    private static float? WaitTime(PlayMakerFSM fsm) {
        var wait = ActiveWait(fsm);
        return wait == null ? null : Traverse.Create(wait).Field("timer").GetValue<float>();
    }

    private static void SetWaitTime(PlayMakerFSM fsm, float seconds) {
        if (ActiveWait(fsm) is not { } wait) return;
        Traverse.Create(wait).Field("timer").SetValue(seconds);
        Traverse.Create(wait).Field("startTime").SetValue(FsmTime.RealtimeSinceStartup - seconds);
    }

    private static HutongGames.PlayMaker.Actions.Wait? ActiveWait(PlayMakerFSM fsm) =>
        fsm.Fsm.ActiveState?.Actions.OfType<HutongGames.PlayMaker.Actions.Wait>().FirstOrDefault();

    /// <summary>This room's own-clock hazards not seen yet; returns how many were added.</summary>
    private static int Look() {
        var added = 0;
        foreach (var animator in UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsSortMode.None)) {
            if (OwnClocks.KindOf(animator.gameObject.name, "") != OwnClocks.Kind.Animator) continue;
            if (Cogs.TryAdd(PathOf(animator.transform), animator)) added++;
        }

        foreach (var fsm in UnityEngine.Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None)) {
            if (OwnClocks.KindOf(fsm.gameObject.name, fsm.FsmName) != OwnClocks.Kind.Loop) continue;
            if (Loops.TryAdd(PathOf(fsm.transform), fsm)) added++;
        }

        // the bursts off as well as on: one off in this game may be on in the scene host's
        foreach (var animator in UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
            if (!animator.gameObject.scene.IsValid() || OwnClocks.KindOf(animator.gameObject.name, "") != OwnClocks.Kind.Burst) continue;
            if (animator.GetComponent<DeactivateAfterDelay>() is not { } off) continue;
            if (Bursts.TryAdd(PathOf(animator.transform), (animator, off))) added++;
        }

        return added;
    }

    private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
}
