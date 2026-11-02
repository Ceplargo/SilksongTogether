using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using SSMP.Api.Client;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BossRule;

/// <summary>
/// BossRule's part in each player's game: holds a boss fight's start until everyone is in the arena (every game runs
/// its own arena, so every game holds), lets a player who goes down in the fight watch, and keeps the room's cutscenes
/// in step (CutsceneFollower).
/// </summary>
internal class BossRuleClientAddon : ClientAddon {
    private readonly IReadOnlyList<BossEntry> _bosses;
    private StartGate? _gate;
    private Spectate? _spectate;
    private ArenaFollower? _follower;
    private CutsceneFollower? _cutscenes;
    private readonly IReadOnlyList<CutsceneEntry> _cutsceneCatalog;
    private RoomEnd? _roomEnd;
    private bool _wantArm;
    private bool _connected;
    private float _nextArmTry;

    public BossRuleClientAddon(IReadOnlyList<BossEntry> bosses, IReadOnlyList<CutsceneEntry> cutscenes) {
        _bosses = bosses;
        _cutsceneCatalog = cutscenes;
    }

    protected override string Name => "BossRule";

    protected override string Version => BossRulePlugin.Version;

    public override bool NeedsNetwork => true;

    public override uint ApiVersion => 1;

    public override void Initialize(IClientApi clientApi) {
        var sender = clientApi.NetClient.GetNetworkSender<BossPacketId>(this);
        var receiver = clientApi.NetClient.GetNetworkReceiver<BossPacketId>(this, BossPacket.Create);
        receiver.RegisterPacketHandler<WaitStatus>(BossPacketId.WaitStatus, packet => {
            if (packet.Message != "") clientApi.UiManager.ChatBox.AddMessage(packet.Message);
        });

        _spectate = new Spectate(clientApi.ClientManager, _bosses,
            scene => sender.SendSingleData(BossPacketId.Downed, new Downed { Scene = scene }),
            scene => sender.SendSingleData(BossPacketId.BossDefeated, new BossDefeated { Scene = scene }));
        receiver.RegisterPacketHandler<FightStarted>(BossPacketId.FightStarted, packet => {
            _spectate.OnFightStarted(packet);
            _gate?.FollowStart(packet.Scene, packet.Flag);
        });
        receiver.RegisterPacketHandler<Outcome>(BossPacketId.Outcome, packet => {
            _spectate.OnOutcome(packet);
            if ((OutcomeKind) packet.Kind == OutcomeKind.Won) _gate?.EndFollowed(packet.Scene);
        });

        // print only (a gate's friend's ground bell up 0.69 s after the host's): each arena follow message with its
        // time in both games, so a late one, or one a second in the same send replaced (single data), shows
        void SendFollow(string scene, string flag, string state) {
            Debug.Log($"[BossRule] ArenaFollow sent: {state} ({scene})");
            var follow = new ArenaFollow { Scene = scene, Flag = flag, State = state };
            sender.SendSingleData(BossPacketId.ArenaFollow, follow);
        }

        _follower = new ArenaFollower(SendFollow);
        _roomEnd = new RoomEnd(SendFollow);
        DoorBells.Send = state => {
            var scene = GameManager.SilentInstance?.sceneName;
            if (_connected && _bosses.FirstOrDefault(e => e.Scene == scene) is { } room) {
                SendFollow(room.Scene, room.Flag, state);
            }
        };
        // A cutscene's states go as collection data, each one numbered: as single data a second state in one send
        // overwrote the first. The server sends them to the room's other players only
        _cutscenes = new CutsceneFollower(_cutsceneCatalog, (scene, name, state, seq) => {
            if (_connected) {
                sender.SendCollectionData(BossPacketId.CutsceneState,
                    new CutsceneState { Scene = scene, Name = name, State = state, Seq = seq });
            }
        }, () => clientApi.ClientManager.IsSceneHost);
        // SSMP hands a collection's items to the handler one by one, in their order
        receiver.RegisterPacketHandler<CutsceneState>(BossPacketId.CutsceneState,
            s => _cutscenes?.Received(s.Scene, s.Name, s.State, s.Seq));
        GroundBells.Send = DoorBells.Send;
        GroundBells.NotSceneHost = () => _connected && !clientApi.ClientManager.IsSceneHost;
        MultiHits.Send = DoorBells.Send;
        MultiHits.NotSceneHost = GroundBells.NotSceneHost;
        MultiHits.SceneHost = () => _connected && clientApi.ClientManager.IsSceneHost;
        MultiHits.Username = () => clientApi.ClientManager.Username;
        MultiHits.PlayerObject = name => clientApi.ClientManager.Players
            .FirstOrDefault(p => p.Username == name && p.IsInLocalScene)?.PlayerObject;
        receiver.RegisterPacketHandler<ArenaFollow>(BossPacketId.ArenaFollow, packet => {
            Debug.Log($"[BossRule] ArenaFollow received: {packet.State} ({packet.Scene})");
            if (GameManager.SilentInstance?.sceneName != packet.Scene) return;
            // another player's hero was caught by a boss's multi-hitter there: the scene host's game sends its connect
            if (packet.State.StartsWith(MultiHits.Prefix)) {
                MultiHits.Received(packet.State.Substring(MultiHits.Prefix.Length));
                return;
            }

            // the scene host's ground bell holder rose: this game's rises too, if it follows the scene host
            if (packet.State.StartsWith(GroundBells.Prefix)) {
                GroundBells.Echo(packet.State.Substring(GroundBells.Prefix.Length));
                return;
            }

            // the door bells shake in every game but the sender's, the scene host's too
            if (packet.State is DoorBells.Start or DoorBells.Stop) {
                DoorBells.Echo(packet.State);
                return;
            }

            // the scene host's intro reached an echo state: every other game runs it on its own arena
            if (packet.State.StartsWith(ArenaFollower.EchoPrefix)) {
                if (_bosses.FirstOrDefault(e => e.Flag == packet.Flag && e.Scene == packet.Scene) is { } echoed) {
                    ArenaFollower.EchoState(echoed, packet.State.Substring(ArenaFollower.EchoPrefix.Length));
                }

                return;
            }

            // another game's room reached its own end (the Widow's, run where a player rang her shrine): this one too,
            // whichever game is the scene host now
            if (packet.State.StartsWith(RoomEnd.Prefix)) {
                if (_bosses.FirstOrDefault(e => e.Flag == packet.Flag && e.Scene == packet.Scene) is { } ended) {
                    RoomEnd.Follow(ended, packet.State.Substring(RoomEnd.Prefix.Length));
                }

                return;
            }

            if (clientApi.ClientManager.IsSceneHost) return;
            if (_bosses.FirstOrDefault(e => e.Flag == packet.Flag && e.Scene == packet.Scene) is { } entry) {
                ArenaFollower.FollowArena(entry, packet.State);
            }
        });

        Marks.Connect(clientApi, mark => sender.SendSingleData(BossPacketId.Mark, mark));
        receiver.RegisterPacketHandler<Mark>(BossPacketId.Mark, Marks.Received);

        _gate = new StartGate(clientApi.ClientManager,
            (scene, message) => sender.SendSingleData(BossPacketId.WaitStatus, new WaitStatus { Scene = scene, Message = message }),
            (entry, others, local) => {
                sender.SendSingleData(BossPacketId.FightStarted, new FightStarted {
                    Scene = entry.Scene, Flag = entry.Flag, Fighters = others, SenderFights = local
                });
                _follower.Watch(entry, StartGate.FindFsm(entry.Start));
            });

        // Every game holds its own arena's start from entering the room (or joining in it) until the fight starts there.
        // A scene host change does not arm it again: the fight may be on, and the server keeps it
        clientApi.ClientManager.ConnectEvent += () => {
            _connected = true;
            _wantArm = true;
            Spectate.Leaving = false;
        };
        clientApi.ClientManager.DisconnectEvent += () => {
            _connected = false;
            StartGate.ForgetRoom();
            _gate.Disarm();
            _follower.Stop();
            _cutscenes?.Stop();
            _wantArm = false;
            _spectate.OnDisconnect();
        };
        // Played alone (no SSMP connection) there is nobody to wait for: no gate, which needs the connection's names.
        // The room's entries are set at once, so a start before the first look arms the gate (StartGate.ArmOnStart)
        SceneManager.activeSceneChanged += (_, next) => {
            _gate.Disarm();
            _follower.Stop();
            _wantArm = !PlaysAlone();
            StartGate.Unarmed = _wantArm ? Unarmed(next.name) : null;
        };

        Logger.Info($"BossRule client add-on registered ({_bosses.Count} bosses)");
    }

    /// <summary>
    /// Once per frame, from the plugin.
    /// </summary>
    public void Tick() {
        if (_gate == null) return;

        // A start that came first armed the gate itself; a join in a room sets the room's entries here
        if (_wantArm && StartGate.Armed != null) _wantArm = false;
        if (_wantArm && StartGate.Unarmed == null && GameManager.SilentInstance is { } gm) {
            StartGate.Unarmed = Unarmed(gm.sceneName);
        }

        // A boss usually lives in its own scene, loaded a moment after the room: keep looking for its start
        if (_wantArm && Time.unscaledTime >= _nextArmTry) {
            _nextArmTry = Time.unscaledTime + 0.5f;
            TryArm();
        }

        _gate.Tick();
        _follower!.Tick();
        if (_connected) _cutscenes!.Tick();
        if (_connected) _roomEnd!.Tick(_bosses, GameManager.SilentInstance?.sceneName);
        _spectate!.Tick();
        GroundBells.Tick();
        MultiHits.Tick();
    }

    /// <summary>
    /// Whether this game plays without an SSMP connection, so holds no boss start: alone, SSMP's client has no name,
    /// and the gate threw on every frame and on every start, so no listed boss of a solo save ever started.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch
    public bool PlaysAlone() => !_connected;

    /// <summary>
    /// Whether this game is in an SSMP session (the mark key works only then).
    /// </summary>
    public bool Connected => _connected;

    /// <summary>
    /// (coop) The gate and a room's catalog entries for StartGate.Unarmed, or null for a room without one (or before the
    /// save is loaded).
    /// </summary>
    private (StartGate, IReadOnlyList<BossEntry>)? Unarmed(string scene) {
        if (_gate == null || PlayerData.instance == null) return null;
        var candidates = BossCatalog.Candidates(_bosses, scene, PlayerData.instance.GetBool);
        return candidates.Count == 0 ? null : (_gate, candidates);
    }

    private void TryArm() {
        var gm = GameManager.SilentInstance;
        if (gm == null || PlayerData.instance == null) return;

        // A room can hold two entries (a black-threaded variant, a rematch): the best one whose start is there
        var candidates = BossCatalog.Candidates(_bosses, gm.sceneName, PlayerData.instance.GetBool);
        if (candidates.Count == 0) {
            _wantArm = false;
            return;
        }

        foreach (var entry in candidates) {
            if (entry.Start.Kind == "battleScene") {
                if (StartGate.FindBattle(entry.Start) is not { } battle) continue;
                _gate!.Arm(entry, null, battle);
            } else {
                if (StartGate.FindFsm(entry.Start) is not { } fsm) continue;
                _gate!.Arm(entry, fsm, null);
            }

            _wantArm = false;
            return;
        }
    }
}
