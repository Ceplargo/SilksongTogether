using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace BossRule;

/// <summary>
/// (0.3.22) Shakra's fight with the room's enemies follows the scene host's game (<see cref="FightRule"/>). There, each
/// state her Attack Enemies enters goes to the other games, her look while it runs (place, facing, shown, clip), and each
/// sound, ring flash and song event as its action runs. A following game leaves out her Idle (its Enemy Range trigger
/// starts no fight of its own) and shows the sent one: each frame's look, held still between frames, and each sent
/// action, played there as it was here. Once it runs the room
/// itself, her Idle is entered again.
/// </summary>
internal static class FightFollow {
    /// <summary>Sends a frame of her fight to the other games; null while not connected.</summary>
    public static System.Action<FightFrame>? Send;

    /// <summary>Whether this game runs the room; null while not connected.</summary>
    public static System.Func<bool>? SceneHost;

    private static string? _scene;
    private static float _nextLook;
    private static PlayMakerFSM? _fight;
    private static bool _skipped;  // her Idle left out here, to be entered once this game runs the room
    private static FightFrame? _frame;  // the scene host's last frame, shown here until her fight ends
    private static (float X, float Y, bool Right, bool Shown, string Clip)? _sent;
    private static float _sentAt;
    private static bool _throwing;  // her disc throw running in this game: the next pooled spawn is her disc
    private static int _throws;  // her discs thrown in this room, each numbered for its frames
    private static readonly Dictionary<int, (GameObject Disc, Vector2 Sent, int Frames)> Thrown = new();  // this game's, sent
    private static readonly Dictionary<int, (GameObject Disc, Vector2 At)> Shown = new();  // the scene host's, shown here
    private static readonly Dictionary<int, int> LastFrame = new();  // each scene host's disc's last frame shown here

    private static bool Hosts => SceneHost?.Invoke() ?? true;
    private static bool Following => FightRule.Follows(Send != null, Hosts);

    /// <summary>Every frame: finds her in a new room; the scene host's game sends her look while she fights, a
    /// following game shows the last frame sent.</summary>
    public static void Tick() {
        try {
            var scene = GameManager.SilentInstance?.sceneName;
            _throwing = false;
            if (scene != _scene) {
                (_scene, _fight, _frame, _sent, _skipped, _nextLook, _throws) = (scene, null, null, null, false, 0f, 0);
                Thrown.Clear();
                Shown.Clear();
                LastFrame.Clear();
            }

            if (_fight == null) {
                if (Time.unscaledTime < _nextLook) return;
                _nextLook = Time.unscaledTime + 1f;
                _fight = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None)
                    .FirstOrDefault(f => f.name == GreetingRule.Npc && f.FsmName == FightRule.Fsm);
                if (_fight == null) return;
            }

            if (Following) {
                if (_frame != null) Show(_frame);
                foreach (var (disc, at) in Shown.Values) Hold(disc, at);
                return;
            }

            if (_skipped) {
                _skipped = false;
                if (_fight.ActiveStateName == "Idle") _fight.SetState("Idle");
            }

            if (Send != null && _fight.ActiveStateName != "Idle") SendLook("", false);
            if (Send != null) SendDiscs();
        } catch (System.Exception e) {
            Debug.LogWarning($"[BossRule] Shakra's fight follow: {e.GetBaseException().Message}");
            _fight = null;
        }
    }

    /// <summary>Before a state is entered: a following game leaves out her Idle; the scene host's game sends her
    /// fight's state.</summary>
    public static bool EnterPrefix(FsmState __instance) {
        try {
            var fsm = __instance.Fsm;
            if (fsm == null || fsm.Name != FightRule.Fsm || fsm.GameObject == null || fsm.GameObject.name != GreetingRule.Npc) {
                return true;
            }

            if (FightRule.SkipsOwn(Following, fsm.Name, __instance.Name)) {
                _skipped = true;
                return false;
            }

            if (Send != null && FightRule.Sends(Hosts, fsm.Name)) SendLook(__instance.Name, true);
        } catch (System.Exception e) {
            Debug.LogWarning($"[BossRule] Shakra's fight state is not sent: {e.GetBaseException().Message}");
        }

        return true;
    }

    private static void SendLook(string state, bool always) {
        if (_fight == null && state == "") return;
        var her = _fight != null ? _fight.gameObject : null;
        if (her == null) return;
        var at = her.transform.position;
        var right = her.transform.localScale.x > 0f;
        var shown = her.GetComponent<MeshRenderer>() is { } renderer && renderer.enabled;
        var clip = her.GetComponent<tk2dSpriteAnimator>()?.CurrentClip?.name ?? "";
        var look = (at.x, at.y, right, shown, clip);
        var looks = _sent is not { } last || last.Right != right || last.Shown != shown || last.Clip != clip;
        var moved = _sent is { } was && (Mathf.Abs(was.X - at.x) > 0.01f || Mathf.Abs(was.Y - at.y) > 0.01f);
        if (!always && !FightRule.SendsFrame(looks, moved, Time.unscaledTime - _sentAt)) return;
        (_sent, _sentAt) = (look, Time.unscaledTime);
        Send?.Invoke(new FightFrame {
            Scene = her.scene.name, State = state, X = at.x, Y = at.y, Right = right, Shown = shown, Clip = clip
        });
    }

    /// <summary>Before one of her sound, ring flash or song-event actions runs in the scene host's game: it goes to the
    /// other games as it runs (some sit after a test in their state: Arriving Voice? speaks only while she enters).</summary>
    public static void ActionPrefix(FsmStateAction __instance) {
        try {
            if (!Sent(__instance)) return;
            if (__instance is FlingObjectsFromGlobalPool) {
                _throwing = true;  // her disc: its flight goes as frames of its own (SpawnPostfix, SendDiscs)
            } else {
                SendAction(__instance);
            }
        } catch (System.Exception e) {
            Debug.LogWarning($"[BossRule] Shakra's fight action is not sent: {e.GetBaseException().Message}");
        }
    }

    private static readonly AccessTools.FieldRef<RandomAudioClipTable, double> NextPlay =
        AccessTools.FieldRefAccess<RandomAudioClipTable, double>("nextPlayTime");

    /// <summary>Before one of her voice lines in the scene host's game: its table's cooldown end, to tell after whether
    /// it spoke (a table speaks by chance and not within its cooldown: shakra_fight's second green, each game's
    /// Shakra_movement on its own dice).</summary>
    public static void VoicePrefix(AudioPlayRandomVoiceFromTableV2 __instance, out double __state) {
        __state = double.NaN;
        try {
            if (Sent(__instance) && __instance.audioClipTable.Value is RandomAudioClipTable table) __state = NextPlay(table);
        } catch (System.Exception e) {
            Debug.LogWarning($"[BossRule] Shakra's fight voice is not sent: {e.GetBaseException().Message}");
        }
    }

    /// <summary>After it: a line it spoke (its table's cooldown started again) goes to the other games.</summary>
    public static void VoicePostfix(AudioPlayRandomVoiceFromTableV2 __instance, double __state) {
        try {
            if (!double.IsNaN(__state) && __instance.audioClipTable.Value is RandomAudioClipTable table && NextPlay(table) != __state) {
                SendAction(__instance);
            }
        } catch (System.Exception e) {
            Debug.LogWarning($"[BossRule] Shakra's fight voice is not sent: {e.GetBaseException().Message}");
        }
    }

    // whether this action is one of her fight's played ones in the scene host's game, to be sent
    private static bool Sent(FsmStateAction action) {
        var fsm = action.Fsm;
        return Send != null && fsm != null && fsm.Name == FightRule.Fsm && fsm.GameObject != null &&
               fsm.GameObject.name == GreetingRule.Npc && FightRule.Sends(Hosts, fsm.Name) && action.State != null &&
               KindOf(fsm, action).Play != FightRule.Play.None;
    }

    private static void SendAction(FsmStateAction action) {
        var state = action.State;
        Send?.Invoke(new FightFrame {
            Scene = action.Fsm.GameObject.scene.name, State = FightRule.ActionTag(state.Name, System.Array.IndexOf(state.Actions, action)),
            Clip = ""
        });
    }

    /// <summary>After the pool spawns an object: the disc her throw just made in the scene host's game is numbered and
    /// its first place sent.</summary>
    public static void SpawnPostfix(GameObject __result) {
        if (!_throwing || __result == null) return;
        _throwing = false;
        try {
            var number = ++_throws;
            Thrown[number] = (__result, new Vector2(float.NaN, float.NaN), 0);
            SendDiscs();
        } catch (System.Exception e) {
            Debug.LogWarning($"[BossRule] Shakra's disc is not sent: {e.GetBaseException().Message}");
        }
    }

    // each of this game's discs that moved since its last frame, or went: its frame to the other games
    private static void SendDiscs() {
        foreach (var (number, (disc, sent, frames)) in Thrown.ToList()) {
            var gone = disc == null || !disc.activeInHierarchy;
            var at = gone ? sent : (Vector2) disc!.transform.position;
            if (!gone && at == sent) continue;
            if (gone) {
                Thrown.Remove(number);
            } else {
                Thrown[number] = (disc!, at, frames + 1);
            }

            Send?.Invoke(new FightFrame {
                Scene = _scene ?? "", State = FightRule.DiscTag(number, frames), X = at.x, Y = at.y, Shown = !gone,
                Clip = ""
            });
        }
    }

    /// <summary>A frame of one of the scene host's discs: shown here where it is there (thrown here first, its hits
    /// left out: the enemy copies' health is the scene host's game's), and put back in the pool once it went.</summary>
    private static void ShowDisc(int number, int seq, FightFrame frame) {
        if (!FightRule.CopiesDiscs()) return;
        int? last = LastFrame.TryGetValue(number, out var l) ? l : null;
        if (!FightRule.Newer(seq, last)) {
            Debug.Log($"[BossRule] Shakra's disc {number}: frame {seq} came after frame {last}, left out");
            return;
        }

        LastFrame[number] = seq;
        Shown.TryGetValue(number, out var shown);
        if (!frame.Shown) {
            if (shown.Disc != null && shown.Disc.activeInHierarchy) shown.Disc.Recycle();
            Shown.Remove(number);
            return;
        }

        var at = new Vector2(frame.X, frame.Y);
        if (shown.Disc == null) {
            var fling = _fight!.FsmStates.SelectMany(s => s.Actions).OfType<FlingObjectsFromGlobalPool>().FirstOrDefault();
            if (fling?.gameObject.Value is not { } prefab) return;
            var z = fling.spawnPoint.Value != null ? fling.spawnPoint.Value.transform.position.z : 0f;
            shown.Disc = prefab.Spawn(new Vector3(at.x, at.y, z), Quaternion.identity);
            var damagers = shown.Disc.GetComponentsInChildren<DamageEnemies>(true).Where(d => d.enabled).ToArray();
            foreach (var damager in damagers) damager.enabled = false;
            shown.Disc.AddComponent<HitsLeftOut>().Damagers = damagers;
        }

        Shown[number] = (shown.Disc, at);
        Hold(shown.Disc, at);
    }

    // a shown disc where the scene host's last frame put it, held still between frames
    private static void Hold(GameObject disc, Vector2 at) {
        if (disc == null || !disc.activeInHierarchy) return;
        disc.transform.position = new Vector3(at.x, at.y, disc.transform.position.z);
        if (disc.GetComponent<Rigidbody2D>() is { } body) body.linearVelocity = Vector2.zero;
    }

    /// <summary>The scene host's frame of her fight: a following game plays the action it names, or shows its look.</summary>
    public static void Received(FightFrame frame) {
        try {
            if (frame.Scene != GameManager.SilentInstance?.sceneName || !Following || _fight == null) return;
            if (FightRule.DiscOf(frame.State) is { } disc) {
                ShowDisc(disc.Number, disc.Frame, frame);
                return;
            }

            if (FightRule.FromTag(frame.State) is { } played) {
                var state = _fight.FsmStates.FirstOrDefault(s => s.Name == played.State);
                if (state != null && played.Index >= 0 && played.Index < state.Actions.Length) {
                    Play(_fight.Fsm, state.Actions[played.Index]);
                }

                return;
            }

            Show(frame);
            _frame = frame.State == "Idle" ? null : frame;  // her fight ended there: she stays where it put her
        } catch (System.Exception e) {
            Debug.LogWarning($"[BossRule] Shakra's fight frame did not play: {e.GetBaseException().Message}");
        }
    }

    private static void Show(FightFrame frame) {
        if (_fight == null) return;
        var her = _fight.gameObject;
        var t = her.transform;
        t.position = new Vector3(frame.X, frame.Y, t.position.z);
        var scale = t.localScale;
        if (scale.x > 0f != frame.Right) t.localScale = new Vector3(-scale.x, scale.y, scale.z);
        if (her.GetComponent<Rigidbody2D>() is { } body) body.linearVelocity = Vector2.zero;  // held still between frames
        if (her.GetComponent<MeshRenderer>() is { } renderer) renderer.enabled = frame.Shown;
        var animator = her.GetComponent<tk2dSpriteAnimator>();
        if (animator != null && frame.Clip != "" && animator.CurrentClip?.name != frame.Clip &&
            animator.GetClipByName(frame.Clip) != null) {
            animator.Play(frame.Clip);
        }
    }

    private static (FightRule.Play Play, GameObject? Target) KindOf(Fsm fsm, FsmStateAction action) {
        var type = action.GetType();
        var owner = type.GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Where(f => f.FieldType == typeof(FsmOwnerDefault))
            .Select(f => f.GetValue(action) as FsmOwnerDefault).FirstOrDefault(o => o != null);
        var atOwner = owner?.OwnerOption == OwnerDefaultOption.UseOwner;
        var target = owner != null ? fsm.GetOwnerDefaultTarget(owner) : null;
        var sendEvent = (type.GetField("sendEvent")?.GetValue(action) as FsmString)?.Value;
        return (FightRule.Of(type.Name, atOwner, target != null ? target.name : null, sendEvent), target);
    }

    private static void Play(Fsm fsm, FsmStateAction action) {
        var (play, target) = KindOf(fsm, action);
        switch (play) {
            case FightRule.Play.Sound when action is AudioPlayRandomVoiceFromTableV2 voice:
                if (target?.GetComponent<AudioSource>() is not { } audio || voice.audioClipTable.Value is not RandomAudioClipTable table) break;
                if (voice.stopPreviousSound) audio.Stop();
                table.PlayOneShot(audio, voice.pitchOffset.Value, true);  // forced: the scene host's game spoke this line
                break;
            case FightRule.Play.Sound:
            case FightRule.Play.Event:
                action.OnEnter();  // as the scene host's game ran it: its own clip, place, pitch and volume; or her song's event
                break;
            case FightRule.Play.Activate:
                target!.SetActive(true);
                break;
        }
    }

    /// <summary>On a disc thrown from another game: its damagers, off while it flies here, back on once it goes back to
    /// the pool (this game may throw it as its own later, once it runs the room).</summary>
    private sealed class HitsLeftOut : MonoBehaviour {
        public DamageEnemies[] Damagers = [];

        private void OnDisable() {
            foreach (var damager in Damagers) {
                if (damager != null) damager.enabled = true;
            }

            Destroy(this);
        }
    }

    /// <summary>The action types whose entry <see cref="ActionPrefix"/> watches: each OnEnter once, where it is declared
    /// (her voice lines have <see cref="VoicePrefix"/> and <see cref="VoicePostfix"/>).</summary>
    public static System.Collections.Generic.IEnumerable<MethodInfo> Watched() =>
        new[] {
            typeof(PlayAudioEvent), typeof(PlayAudioEventRandom), typeof(FlingObjectsFromGlobalPool),
            typeof(HutongGames.PlayMaker.Actions.ActivateGameObject), typeof(HutongGames.PlayMaker.Actions.SendEventByName),
            typeof(SetFsmBool)
        }.Select(t => t.GetMethod("OnEnter", BindingFlags.Public | BindingFlags.Instance)!.DeclaringType!)
         .Distinct()
         .Select(t => t.GetMethod("OnEnter", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)!);
}
