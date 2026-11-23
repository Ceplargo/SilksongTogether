using System.IO;
using BepInEx;
using HarmonyLib;
using HutongGames.PlayMaker;
using SSMP.Api.Client;
using SSMP.Api.Server;

namespace BossRule;

/// <summary>
/// Bosses wait until every player is in the arena; a player who dies in a boss fight watches until it is won or
/// everyone is down. Registers BossRule's SSMP client and server add-ons, which every player must have.
/// </summary>
[BepInPlugin("bossrule", "BossRule", BossRulePlugin.Version)]
[BepInDependency("ssmp")]
public class BossRulePlugin : BaseUnityPlugin {
    /// <summary>
    /// The co-op zip's version (make_release.py builds no other), sent as BossRule's add-on version at login: SSMP's
    /// server refuses a friend whose version differs, since synced values travel as positions in a list built into
    /// the mod.
    /// </summary>
    internal const string Version = "0.4.0";

    private BossRuleClientAddon? _client;

    private void Awake() {
        var folder = Path.GetDirectoryName(Info.Location)!;
        var catalog = Path.Combine(folder, "bosses.json");
        var bosses = File.Exists(catalog) ? BossCatalog.Load(File.ReadAllText(catalog)) : [];
        var cutsceneCatalog = Path.Combine(folder, "cutscenes.json");
        var cutscenes = File.Exists(cutsceneCatalog) ? CutsceneCatalog.Load(File.ReadAllText(cutsceneCatalog)) : [];
        _client = new BossRuleClientAddon(bosses, cutscenes);
        ClientAddon.RegisterAddon(_client);
        ServerAddon.RegisterAddon(new BossRuleServerAddon());
        Marks.Init(Config, Logger, folder);

        var harmony = new Harmony("bossrule");
        harmony.Patch(AccessTools.Method(typeof(Fsm), nameof(Fsm.ProcessEvent), [typeof(FsmEvent), typeof(FsmEventData)]),
            prefix: new HarmonyMethod(typeof(StartGate), nameof(StartGate.ProcessEventPrefix)));
        // (coop) the Lava Challenge's door waits up to WaitRule.RoomWaitS for every hero in its room (LavaHold)
        harmony.Patch(AccessTools.Method(typeof(Fsm), nameof(Fsm.ProcessEvent), [typeof(FsmEvent), typeof(FsmEventData)]),
            prefix: new HarmonyMethod(typeof(LavaHold), nameof(LavaHold.ProcessEventPrefix)));
        // (coop) an ordinary arena a hero walks into waits up to WaitRule.RoomWaitS for every hero in its room, before the
        // others (ArenaWaves' and ArenaStarts' sends check that it ran)
        harmony.Patch(AccessTools.Method(typeof(BattleScene), nameof(BattleScene.StartBattle)),
            prefix: new HarmonyMethod(typeof(ArenaHold), nameof(ArenaHold.StartBattlePrefix)) { priority = Priority.First });
        harmony.Patch(AccessTools.Method(typeof(BattleScene), "OnTriggerEnter2D"),
            prefix: new HarmonyMethod(typeof(ArenaHold), nameof(ArenaHold.TriggerPrefix)),
            finalizer: new HarmonyMethod(typeof(ArenaHold), nameof(ArenaHold.TriggerFinalizer)));
        harmony.Patch(AccessTools.Method(typeof(BattleScene), nameof(BattleScene.StartBattle)),
            prefix: new HarmonyMethod(typeof(StartGate), nameof(StartGate.StartBattlePrefix)));
        harmony.Patch(AccessTools.Method(typeof(BattleScene), nameof(BattleScene.DoEndBattle)),
            prefix: new HarmonyMethod(typeof(ArenaWins), nameof(ArenaWins.EndPrefix)));
        harmony.Patch(AccessTools.Method(typeof(BattleScene), nameof(BattleScene.StartBattle)),
            prefix: new HarmonyMethod(typeof(ArenaWaves), nameof(ArenaWaves.StartBattlePrefix)));
        // (coop) an ordinary arena started here by this game's own act starts in every game in its room (the owner, 2026-11-20)
        harmony.Patch(AccessTools.Method(typeof(BattleScene), nameof(BattleScene.StartBattle)),
            prefix: new HarmonyMethod(typeof(ArenaStarts), nameof(ArenaStarts.StartBattlePrefix)),
            postfix: new HarmonyMethod(typeof(ArenaStarts), nameof(ArenaStarts.StartBattlePostfix)));
        // (coop) each game shuts an ordinary arena's gates only as its own hero walks in (the owner, 2026-11-20)
        harmony.Patch(AccessTools.Method(typeof(BattleScene), nameof(BattleScene.LockInBattle)),
            prefix: new HarmonyMethod(typeof(ArenaGates), nameof(ArenaGates.LockPrefix)));
        harmony.Patch(AccessTools.Method(typeof(Gate), nameof(Gate.ForceClose)),
            prefix: new HarmonyMethod(typeof(ArenaGates), nameof(ArenaGates.GateClosePrefix)));
        harmony.Patch(AccessTools.Method(typeof(BattleScene), "StartWave"),
            prefix: new HarmonyMethod(typeof(ArenaWaves), nameof(ArenaWaves.StartWavePrefix)));
        harmony.Patch(AccessTools.Method(typeof(BattleScene), nameof(BattleScene.WaveEnd)),
            prefix: new HarmonyMethod(typeof(ArenaWaves), nameof(ArenaWaves.WaveEndPrefix)));
        // (coop, 0.4) a wave started while following lost its start; given again at the takeover (ArenaWaves.WakeLost)
        harmony.Patch(AccessTools.Method(typeof(BattleWave), nameof(BattleWave.WaveStarted)),
            postfix: new HarmonyMethod(typeof(ArenaWaves), nameof(ArenaWaves.WaveStartedPostfix)));
        harmony.Patch(AccessTools.Method(typeof(HutongGames.PlayMaker.Actions.Trigger2dEvent),
                nameof(HutongGames.PlayMaker.Actions.Trigger2dEvent.DoTriggerStay2D)),
            prefix: new HarmonyMethod(typeof(GroundBells), nameof(GroundBells.TriggerStayPrefix)));
        harmony.Patch(AccessTools.Method(typeof(HutongGames.PlayMaker.Actions.SendEventByName),
                nameof(HutongGames.PlayMaker.Actions.SendEventByName.OnEnter)),
            postfix: new HarmonyMethod(typeof(MultiHits), nameof(MultiHits.SentPostfix)));
        harmony.Patch(AccessTools.Method(typeof(PlayMakerFSM), "OnDisable"),
            prefix: new HarmonyMethod(typeof(TrapFollow), nameof(TrapFollow.DisablePrefix)));
        harmony.Patch(AccessTools.Method(typeof(HutongGames.PlayMaker.Actions.Wait), "OnUpdate"),
            prefix: new HarmonyMethod(typeof(TrapFollow), nameof(TrapFollow.WaitPrefix)));
        // 0.4 world Task 16: a followed lava hazard takes the scene host's flip
        harmony.Patch(AccessTools.Method(typeof(RandomlyFlipScale), nameof(RandomlyFlipScale.ApplyScale)),
            prefix: new HarmonyMethod(typeof(ClockFollow), nameof(ClockFollow.FlipPrefix)));
        harmony.Patch(AccessTools.Method(typeof(ActivateChildrenOnContact), "OnTriggerEnter2D"),
            postfix: new HarmonyMethod(typeof(ContactFollow), nameof(ContactFollow.EnterPostfix)));
        harmony.Patch(AccessTools.Method(typeof(TempPressurePlate), "PreActivate"),
            postfix: new HarmonyMethod(typeof(PlateFollow), nameof(PlateFollow.PreActivatePostfix)));
        harmony.Patch(AccessTools.Method(typeof(BellShrineGateLock), nameof(BellShrineGateLock.StartedPlaying)),
            postfix: new HarmonyMethod(typeof(DoorBells), nameof(DoorBells.StartedPostfix)));
        harmony.Patch(AccessTools.Method(typeof(BellShrineGateLock), nameof(BellShrineGateLock.StoppedPlaying)),
            postfix: new HarmonyMethod(typeof(DoorBells), nameof(DoorBells.StoppedPostfix)));
        // 0.4 world Task 17: a followed heart's reward is its leader's (CutsceneFollower.Reward)
        harmony.Patch(AccessTools.Method(typeof(HeroController), nameof(HeroController.AddToMaxSilkRegen)),
            prefix: new HarmonyMethod(typeof(CutsceneFollower), nameof(CutsceneFollower.RewardPrefix)));
        harmony.Patch(AccessTools.Method(typeof(HeroController), "Die", [typeof(bool), typeof(bool)]),
            prefix: new HarmonyMethod(typeof(Spectate), nameof(Spectate.DiePrefix)));
        // The game heads for the main menu (the player's quit, or SSMP after a lost connection): a watcher does not die
        harmony.Patch(AccessTools.Method(typeof(UIManager), nameof(UIManager.ReturnToMainMenu)),
            prefix: new HarmonyMethod(typeof(Spectate), nameof(Spectate.LeavingPrefix)));
        harmony.Patch(AccessTools.Method(typeof(GameManager), nameof(GameManager.ReturnToMainMenu)),
            prefix: new HarmonyMethod(typeof(Spectate), nameof(Spectate.LeavingPrefix)));
        harmony.Patch(AccessTools.Method(typeof(CollectableItemPickup), nameof(CollectableItemPickup.SetItem)),
            postfix: new HarmonyMethod(typeof(DropShare), nameof(DropShare.SetItemPostfix)));
        harmony.Patch(AccessTools.Method(typeof(PersistentEnemyItemDrop), "DropItem"),
            postfix: new HarmonyMethod(typeof(DropShare), nameof(DropShare.DropItemPostfix)));
        harmony.Patch(AccessTools.Method(typeof(PlayAudioEventBase), nameof(PlayAudioEventBase.OnEnter)),
            postfix: new HarmonyMethod(typeof(TollFollow), nameof(TollFollow.AudioPostfix)));
        harmony.Patch(AccessTools.Method(typeof(FsmState), nameof(FsmState.OnEnter)),
            prefix: new HarmonyMethod(typeof(GreetingFollow), nameof(GreetingFollow.EnterPrefix)));
        harmony.Patch(AccessTools.Method(typeof(FsmState), nameof(FsmState.OnEnter)),
            prefix: new HarmonyMethod(typeof(FightFollow), nameof(FightFollow.EnterPrefix)));
        foreach (var onEnter in FightFollow.Watched()) {
            harmony.Patch(onEnter, prefix: new HarmonyMethod(typeof(FightFollow), nameof(FightFollow.ActionPrefix)));
        }
        harmony.Patch(AccessTools.Method(typeof(ObjectPool), nameof(ObjectPool.Spawn),
                [typeof(UnityEngine.GameObject), typeof(UnityEngine.Transform), typeof(UnityEngine.Vector3), typeof(UnityEngine.Quaternion), typeof(bool)]),
            postfix: new HarmonyMethod(typeof(FightFollow), nameof(FightFollow.SpawnPostfix)));
        harmony.Patch(AccessTools.Method(typeof(HutongGames.PlayMaker.Actions.AudioPlayRandomVoiceFromTableV2), "OnEnter"),
            prefix: new HarmonyMethod(typeof(FightFollow), nameof(FightFollow.VoicePrefix)),
            postfix: new HarmonyMethod(typeof(FightFollow), nameof(FightFollow.VoicePostfix)));
        harmony.Patch(AccessTools.Method(typeof(NPCSpeakingAudio), "PlayVoice",
                [typeof(RandomAudioClipTable), typeof(UnityEngine.Vector3), typeof(UnityEngine.AudioSource), typeof(NPCSpeakingAudio)]),
            prefix: new HarmonyMethod(typeof(GreetingFollow), nameof(GreetingFollow.VoicePrefix)));
        DialogueHooks.Apply(harmony);
        HiddenAreas.Apply(harmony);
        DialogueOverlay.Make();
        // SSMP's two copy dispatchers: a mark's record has the last 10 s of copied actions
        var ssmp = typeof(ClientAddon).Assembly;
        var dispatchers = ssmp.GetType("SSMP.Game.Client.Entity.Action.EntityFsmActions", true);
        var data = ssmp.GetType("SSMP.Networking.Packet.Data.EntityNetworkData", true);
        harmony.Patch(AccessTools.Method(dispatchers, "GetNetworkDataFromAction", [data, typeof(FsmStateAction)]),
            postfix: new HarmonyMethod(typeof(Marks), nameof(Marks.SentPostfix)));
        harmony.Patch(AccessTools.Method(dispatchers, "ApplyNetworkDataFromAction", [data, typeof(FsmStateAction)]),
            prefix: new HarmonyMethod(typeof(Marks), nameof(Marks.AppliedPrefix)));

        Logger.LogInfo($"BossRule loaded, {bosses.Count} bosses and {cutscenes.Count} cutscenes in the catalogs");
    }

    private void Update() {
        MultiHits.Frames.Add(UnityEngine.Time.unscaledTime, UnityEngine.Time.unscaledDeltaTime); // (coop, 0.3.23) a catch's freeze
        _client?.Tick();
        if (_client?.Connected == true) CaravanRespawn.Tick();
        Marks.Tick(_client?.Connected == true);
        QuestRewards.Tick();
        GreetingFollow.Tick();
        FightFollow.Tick();
    }
}
