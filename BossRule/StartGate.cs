using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HutongGames.PlayMaker;
using SSMP.Api.Client;
using UnityEngine;

namespace BossRule;

/// <summary>
/// Holds a listed boss fight's start in every player's game until every connected player is in the arena: each game
/// runs its own arena (its gates and start ranges are no SSMP entities), so a start held only where the boss runs would
/// let another game's arena start and close its gates in front of a player still on the way. Two Harmony prefixes do
/// the holding: on <c>Fsm.ProcessEvent</c>, which every PlayMaker event an FSM receives goes through, and on
/// <c>BattleScene.StartBattle</c> for wave arenas. With the host's wait rule off it holds nothing. The scene host's game
/// (whose game runs the boss) shows the waiting line, and at the start sets the boss's health and tells the server.
/// </summary>
internal sealed class StartGate {
    /// <summary>
    /// The armed gate, for the prefixes. One room at a time.
    /// </summary>
    public static StartGate? Armed { get; private set; }

    /// <summary>
    /// (coop) From a room's start (or the join) until a gate is armed: the gate to arm and the room's catalog entries.
    /// The look for a start's FSM runs every 0.5 s (BossRuleClientAddon.TryArm); a start that comes first arms the gate
    /// on the spot (<see cref="ArmOnStart"/>).
    /// </summary>
    public static (StartGate Gate, IReadOnlyList<BossEntry> Entries)? Unarmed { get; set; }

    /// <summary>
    /// (coop) A disconnect forgets the room's entries: they stayed, so a start event in the moments before the menu armed
    /// a gate whose wait rule needs the connection. Not inlined: disconnect_start's twin skips it.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ForgetRoom() => Unarmed = null;

    private readonly IClientManager _client;
    private readonly Action<string, string> _sendStatus;
    private readonly Action<BossEntry, ushort[], bool> _fightStarted;
    private BossEntry? _entry;
    private PlayMakerFSM? _fsm;
    private BattleScene? _battle;
    private HashSet<string> _states = new();
    private HashSet<string> _events = new();
    private List<Collider2D> _area = new();
    private string? _pendingEvent;
    private bool _pendingBattle;
    private string _scene = "";
    private string _message = "";
    private BattleScene? _followedBattle;
    private string _followedScene = "";
    private bool _shutOnward;

    /// <param name="client">SSMP's client manager.</param>
    /// <param name="sendStatus">Sends the waiting line for a scene to the server ("" once nobody is missing).</param>
    /// <param name="fightStarted">Called when a fight starts: the entry, the other players in the arena, and whether
    /// the local player is in it.</param>
    public StartGate(IClientManager client, Action<string, string> sendStatus, Action<BossEntry, ushort[], bool> fightStarted) {
        _client = client;
        _sendStatus = sendStatus;
        _fightStarted = fightStarted;
    }

    public BossEntry? Entry => _entry;

    /// <summary>
    /// Hold this entry's fight start: its FSM events (fsm entries) or its battle (battleScene entries).
    /// </summary>
    public void Arm(BossEntry entry, PlayMakerFSM? fsm, BattleScene? battle) {
        Disarm();
        _entry = entry;
        _fsm = fsm;
        _battle = battle;
        _states = new HashSet<string>((entry.Start.State ?? "").Split('|'));
        _events = new HashSet<string>((entry.Start.Event ?? "").Split('|'));
        _area = Presence.AreaColliders(entry);
        _scene = entry.Scene;
        _shutOnward = false;
        Armed = this;
        Unarmed = null;
        Debug.Log($"[BossRule] Holding the start of {entry.Boss} ({entry.Flag}) until everyone is in the arena");
    }

    /// <summary>
    /// The fight is on in the scene host's game (the server says so): an arena still waiting here starts too, since its
    /// own start can happen in one game only (a trap bench broken in the host's game, a boss's own FSM starting its
    /// battle). This game holds it as any start until everyone is in.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch
    public void FollowStart(string scene, string flag) {
        if (_entry == null || _entry.Scene != scene || _entry.Flag != flag) return;
        if (LocalOutside()) return;

        if (_battle != null) {
            _followedBattle = _battle;
            _followedScene = scene;
            _battle.StartBattle();
        } else if (_fsm != null && _states.Contains(_fsm.ActiveStateName) &&
                   (_entry.Start.Event ?? "").Split('|')[0] is { Length: > 0 } start) {
            _fsm.SendEvent(start);
        }

        Debug.Log($"[BossRule] The fight with {_entry?.Boss ?? flag} is on in the scene host's game: this arena follows");
    }

    /// <summary>
    /// (coop) This game's hero takes input again while this game holds the start of a boss whose start leaves the hero
    /// held (bosses.json's release) for the players not in yet: the Widow's talk ends without giving the hero back and only
    /// her roar does, which waits with her start, so the talker stood held until the last player came in. (A game that
    /// does not run her gives its hero back at her talk's end: CutsceneFollower.Release.) Not while the hero changes room,
    /// respawns or is dead.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch
    public static void Release(string why) {
        if (HeroController.SilentInstance is not { acceptingInput: false } hero ||
            hero.cState.transitioning || hero.cState.hazardRespawning || hero.cState.dead) return;
        hero.RegainControl();
        Debug.Log($"[BossRule] The hero takes input again: {why}");
    }

    /// <summary>
    /// This game's player is outside the arena (the wait rule off lets a fight start without it): its arena stays
    /// open, so its gates do not shut it out.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch
    public bool LocalOutside() =>
        _entry != null && !Presence.Collect(_entry, _client, _area).Any(p => p.Id == Presence.LocalId && p.InArea);

    /// <summary>
    /// The fight was won: a wave arena this game started only because the scene host's had (FollowStart) ends here too,
    /// and so does any other wave arena of the room still fighting here (Bone_East_25's starts from its boss's own FSM).
    /// Their waves count SSMP's hidden originals of the enemies, which never leave, so they never ended by themselves
    /// and kept the friend shut in after the fight.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch
    public void EndFollowed(string scene) {
        var followed = _followedScene == scene ? _followedBattle : null;
        _followedBattle = null;
        if (_client.IsSceneHost) return;  // its arenas end by their own count; a sweep could cut a later wave short

        var battles = UnityEngine.Object.FindObjectsByType<BattleScene>(FindObjectsSortMode.None)
            .Where(b => b == followed || InRoom(b, scene)).ToList();
        var ended = battles.Where(b => (b == followed || BattleStarted(b)) && !BattleCompleted(b)).ToList();
        foreach (var battle in ended) battle.DoEndBattle();
        foreach (var battle in battles.Except(ended).Where(b => b.isActiveAndEnabled && b.openGatesOnEnd)) {
            battle.StartCoroutine(OpenWhenCompleted(battle));
        }

        Debug.Log($"[BossRule] The fight in {scene} was won: {ended.Count} wave arenas here end with the scene host's");
    }

    /// <summary>
    /// A wave arena the shared save completes here (the scene host's end, synced) sends only the BG QUICK OPEN of a room
    /// loaded after its battle, which some gates do not take (Bone_East_25's ant gate waits for BG OPEN): once it is
    /// completed (the save can come after the win), its gates get the BG OPEN the scene host's had. Open gates ignore it.
    /// </summary>
    private static IEnumerator OpenWhenCompleted(BattleScene battle) {
        for (var waited = 0f; waited < 15f && !BattleCompleted(battle); waited += Time.deltaTime) yield return null;
        if (BattleCompleted(battle)) battle.SendEventToChildren("BG OPEN");
    }

    internal static readonly AccessTools.FieldRef<BattleScene, bool> BattleStarted =
        AccessTools.FieldRefAccess<BattleScene, bool>("started");

    internal static readonly AccessTools.FieldRef<BattleScene, bool> BattleCompleted =
        AccessTools.FieldRefAccess<BattleScene, bool>("completed");

    /// <summary>The room's own scene or one of its additive scenes (its boss scene: Bone_East_08_boss_golem).</summary>
    private static bool InRoom(Component c, string scene) =>
        c.gameObject.scene.name == scene || c.gameObject.scene.name.StartsWith(scene + "_");

    /// <summary>
    /// (coop) A held start shuts the catalog's onward objects named for the door this game's hero came in by (Onward):
    /// a battle gate by BG CLOSE, anything else switched on (the Last Judge's Gate L, Ant_19's Top Blocker), as the
    /// fight's own start would. The hold stops that start, so a way onward stayed open while the fight waited and a
    /// player could walk past the waiting boss (round three's walk-past sweep). Only what the start itself shuts is
    /// named, so the win opens it again as usual; the way in is never named, in the second player's game either. Not
    /// inlined: walk_past's twin switches it off with a patch.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ShutsOnward() {
        if (_shutOnward || _entry?.Onward is not { Count: > 0 } onward) return;
        _shutOnward = true;
        var entryGate = GameManager.SilentInstance is { } gm ? gm.entryGateName : null;
        var shut = Onward.Shut(onward, entryGate);
        var found = shut.SelectMany(SceneObjects.Named)
            .Where(go => InRoom(go.transform, _scene) && (go.transform.parent == null || go.transform.parent.gameObject.activeInHierarchy))
            .ToList();
        foreach (var go in found) {
            var gate = go.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "BG Control");
            if (gate != null) gate.SendEvent("BG CLOSE");
            else go.SetActive(true);
        }

        Debug.Log($"[BossRule] Holding {_entry.Boss}: shut onward from {entryGate ?? "an unknown door"}: " +
                  $"{string.Join(", ", found.Select(go => go.name))}");
    }

    public void Disarm() {
        if (_message != "") _sendStatus(_scene, "");
        _message = "";
        _entry = null;
        _fsm = null;
        _battle = null;
        _pendingEvent = null;
        _pendingBattle = false;
        if (Armed == this) Armed = null;
    }

    /// <summary>
    /// Once per frame: tell the room who is missing when that changes, and let a held start go once nobody is.
    /// </summary>
    public void Tick() {
        if (_entry == null) return;

        var result = Evaluate();
        // One line per held start, from the game that runs the boss (the other games hold their own arenas quietly)
        var holding = _pendingEvent != null || _pendingBattle;
        var message = holding && WaitRuleOn && !result.Allowed && _client.IsSceneHost ? WaitRule.Message(result) : "";
        if (message != _message) {
            _message = message;
            _sendStatus(_scene, message);
        }

        if (!MayStart(result)) return;

        if (_pendingEvent != null) {
            var held = _pendingEvent;
            _pendingEvent = null;
            Debug.Log($"[BossRule] Letting {held} go to {_fsm!.FsmName} in {_fsm.ActiveStateName}");
            _fsm.SendEvent(held);
        } else if (_pendingBattle) {
            _pendingBattle = false;
            _battle!.StartBattle();
        }
    }

    private bool WaitRuleOn => _client.ServerSettings.BossWaitRule;

    private bool MayStart() => MayStart(Evaluate());

    private bool MayStart(WaitResult result) => WaitRuleOn ? result.Allowed : !KeepsOutsideOpen();

    /// <summary>
    /// With the wait rule off, a game that does not run the boss keeps its arena open while its player is outside it,
    /// and lets the held start go once the player walks in: an arena's own start-range check also went off in the
    /// friend's game when the scene host's player walked in (the Savage Beastfly's), so its gates shut the friend out.
    /// The scene host's game starts as before, since the boss fights from there.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch
    public bool KeepsOutsideOpen() => !_client.IsSceneHost && LocalOutside();

    private WaitResult Evaluate() {
        if (_area.Any(c => c == null)) _area = Presence.AreaColliders(_entry!);
        return WaitRule.Evaluate(Presence.Collect(_entry!, _client, _area));
    }

    /// <summary>
    /// The fight is on; nothing more to hold.
    /// </summary>
    private void Started() {
        var entry = _entry!;
        var presence = Presence.Collect(entry, _client, _area);
        Debug.Log($"[BossRule] The fight with {entry.Boss} ({entry.Flag}) starts");
        Disarm();
        // The other games only let their own arena go; the boss and the server follow the scene host's game
        if (!_client.IsSceneHost) return;

        ScaleBoss(entry, presence.Count(p => p.InArea));
        _fightStarted(entry, presence.Where(p => p.InArea && p.Id != Presence.LocalId).Select(p => p.Id).ToArray(),
            presence.Any(p => p.InArea && p.Id == Presence.LocalId));
    }

    /// <summary>
    /// The boss's health and phase switch points for this fight, by the host's settings and the players in the arena.
    /// Each body gets its base health from the catalog (what its room gives it at load; the game's own starting value,
    /// <c>initHp</c>, can be lower, as the Clover Dancers' 300 under their room's 580) times the multiplier, never the
    /// current health: SSMP gives a re-entered room its last stored enemy health, so scaling that would compound with
    /// every retry. SSMP's health sync gives the other games the new health.
    /// </summary>
    private void ScaleBoss(BossEntry entry, int players) {
        var settings = _client.ServerSettings;
        var multiplier = BossScaling.Multiplier(players, settings.BossHealthPerPlayer, settings.BossHealthFixed);
        if (Math.Abs(multiplier - 1f) < 0.001f) return;

        foreach (var body in entry.Boss.Split('|').SelectMany(SceneObjects.Named)
                     .Where(go => !go.name.EndsWith("(Clone)")).Select(go => go.GetComponent<HealthManager>()).Where(h => h != null)) {
            body.hp = BossScaling.ScaleHp(entry.BaseHp(body.gameObject.name), multiplier);
        }

        ScalePhases(entry, multiplier);
        Debug.Log($"[BossRule] {entry.Boss}: health x{multiplier} for {players} players");
    }

    /// <summary>
    /// The boss's phase switch points for this fight's health: each catalog phase's FSM variable, or the fixed number
    /// in its action (Fourth Chorus's), set to its base value times the multiplier.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch
    private static void ScalePhases(BossEntry entry, float multiplier) {
        foreach (var phase in entry.Phases) {
            var fsm = SceneObjects.Named(phase.Object).Where(go => !go.name.EndsWith("(Clone)"))
                .SelectMany(go => go.GetComponents<PlayMakerFSM>()).FirstOrDefault(f => f.FsmName == phase.Fsm);
            if (fsm == null) continue;

            var value = BossScaling.ScaleThreshold(phase.Value, multiplier);
            var number = phase.Var != null
                ? fsm.FsmVariables.GetVariable(phase.Var)
                : fsm.FsmStates.FirstOrDefault(state => state.Name == phase.State)?.Actions
                    .FirstOrDefault(action => action.GetType().Name == phase.Action) is { } found
                    ? found.GetType().GetField(phase.Field ?? "")?.GetValue(found)
                    : null;
            switch (number) {
                case FsmInt whole:
                    whole.Value = (int) Math.Round(value, MidpointRounding.AwayFromZero);
                    break;
                case FsmFloat real:
                    real.Value = value;
                    break;
            }
        }
    }

    /// <summary>
    /// The start FSM an entry names: on an object the catalog names, with that FSM name and one of its start states;
    /// an active original before an inactive one or a pool copy.
    /// </summary>
    public static PlayMakerFSM? FindFsm(BossStart start) {
        var states = (start.State ?? "").Split('|');
        return SceneObjects.Named(start.Object!).SelectMany(go => go.GetComponents<PlayMakerFSM>())
            .Where(f => f.FsmName == start.Fsm && f.FsmStates.Any(s => states.Contains(s.Name)))
            .OrderByDescending(f => f.gameObject.activeInHierarchy)
            .ThenBy(f => f.gameObject.name.EndsWith("(Clone)"))
            .FirstOrDefault();
    }

    public static BattleScene? FindBattle(BossStart start) =>
        SceneObjects.Named(start.Object!).Select(go => go.GetComponent<BattleScene>()).Where(b => b != null)
            .OrderByDescending(b => b.gameObject.activeInHierarchy).FirstOrDefault();

    /// <summary>
    /// (coop) A fight's start (its FSM's start event in a start state, or its BattleScene's start) that comes before the
    /// 0.5 s look found it arms the gate on it, so it is held as any start: with a player in the start range as the
    /// boss scene came in, the Vampire Gnat's fight began before the look, unheld (nobody waited for the friend) and at
    /// its base health and switch points. The FSM's name is compared first: this runs for every FSM event until a gate
    /// is armed in a boss room.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch
    public static StartGate? ArmOnStart(Fsm? fsm, FsmEvent? fsmEvent, BattleScene? battle) {
        if (Unarmed is not { } unarmed) return null;
        var entries = unarmed.Entries;
        for (var i = 0; i < entries.Count; i++) {
            var entry = entries[i];
            var start = entry.Start;
            if (battle != null) {
                if (start.Kind != "battleScene" || !SceneObjects.Is(battle.gameObject, start.Object!)) continue;
                unarmed.Gate.Arm(entry, null, battle);
                return Armed;
            }

            if (start.Kind == "battleScene" || fsm == null || fsmEvent == null || start.Fsm != fsm.Name ||
                fsm.Owner is not PlayMakerFSM owner || !start.StartsOn(fsm.ActiveStateName, fsmEvent.Name) ||
                !SceneObjects.Is(owner.gameObject, start.Object!)) {
                continue;
            }

            unarmed.Gate.Arm(entry, owner, null);
            return Armed;
        }

        return null;
    }

    /// <summary>
    /// Prefix on <c>Fsm.ProcessEvent(FsmEvent, FsmEventData)</c>: swallow a start event while players are missing.
    /// </summary>
    public static bool ProcessEventPrefix(Fsm __instance, FsmEvent fsmEvent) {
        var gate = Armed ?? (Unarmed == null ? null : ArmOnStart(__instance, fsmEvent, null));
        if (gate?._fsm == null || fsmEvent == null || __instance != gate._fsm.Fsm) return true;
        if (!gate._states.Contains(__instance.ActiveStateName) || !gate._events.Contains(fsmEvent.Name)) return true;

        if (gate.MayStart()) {
            gate.Started();
            return true;
        }

        gate._pendingEvent = fsmEvent.Name;
        Debug.Log($"[BossRule] Holding {fsmEvent.Name} in {__instance.ActiveStateName} of {__instance.Name}");
        gate.ShutsOnward();
        if (gate._entry!.Release) Release("the fight's start waits for the players not in yet");
        return false;
    }

    /// <summary>
    /// Prefix on <c>BattleScene.StartBattle</c>: hold a wave arena's start while players are missing.
    /// </summary>
    public static bool StartBattlePrefix(BattleScene __instance) {
        var gate = Armed ?? (Unarmed == null ? null : ArmOnStart(null, null, __instance));
        if (gate?._battle == null || __instance != gate._battle) return true;

        if (gate.MayStart()) {
            gate.Started();
            return true;
        }

        gate._pendingBattle = true;
        gate.ShutsOnward();
        return false;
    }
}
