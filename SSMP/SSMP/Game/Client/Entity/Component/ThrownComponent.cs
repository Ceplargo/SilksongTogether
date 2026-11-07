using System.Collections.Generic;
using HutongGames.PlayMaker.Actions;
using SSMP.Networking.Client;
using SSMP.Networking.Packet.Data;
using SSMP.Util;
using UnityEngine;
using Logger = SSMP.Logging.Logger;

namespace SSMP.Game.Client.Entity.Component;

/// <inheritdoc />
/// This component makes what an enemy spawns from the pool (its thrown attacks, and the visible effects that go with
/// them) fly, and end, as the scene host's do. SSMP copies the spawn, so each game spawns its own at the scene host's
/// place, but each then ran its own: a random spread, a direction or aim picked in the scene host's game alone, or a
/// random fuse came out differently (Trobbio's bombs and tornadoes, the Vampire Gnat's sickle). The scene host numbers
/// each such object and, about 15 times a second (and at once when one would be off or changes state), sends where the
/// live ones are, how fast they move, and the state and float variables of their FSMs; the other games move their copy
/// there, predicted to now, each frame after the copy's own FSM and physics have run, and give its FSMs the host's
/// values and each new state the host's enters.
internal class ThrownComponent : EntityComponent {
    /// <summary>
    /// Seconds between the scene host's regular updates.
    /// </summary>
    private const float SendInterval = 1f / 15f;

    /// <summary>
    /// Seconds past the last update a copy is still moved; after that (a lost update, or the scene host gone) the
    /// copy goes on by itself.
    /// </summary>
    private const float MaxPrediction = 0.25f;

    /// <summary>
    /// Units the other games' copy may be off the scene host's (by what they predict from the last update) before an
    /// update goes out at once: a sickle turning back or a bomb bouncing would otherwise be up to 3 units off until the
    /// next regular update.
    /// </summary>
    private const float MaxError = 0.5f;

    /// <summary>
    /// Least seconds between two updates.
    /// </summary>
    private const float MinInterval = 1f / 60f;

    /// <summary>
    /// The most FSMs of an object, and float variables of an FSM, an update carries.
    /// </summary>
    private const int MaxFsms = 4;

    private const int MaxFloats = 32;

    /// <summary>
    /// (coop) The ground's layer, which a solid copy's predicted move must not cross.
    /// </summary>
    private static readonly int TerrainMask = LayerMask.GetMask("Terrain");

    /// <summary>
    /// A state index for an FSM with no known state.
    /// </summary>
    private const byte NoState = byte.MaxValue;

    /// <summary>
    /// Numbers from here up are the stage objects below; spawns take 1 up to here.
    /// </summary>
    private const byte FirstStageNumber = 200;

    /// <summary>
    /// Stage objects an enemy's fight sets off whose own FSMs pick at random, which every game ran on its own: by the
    /// enemy's name, the objects under the enemy's parent whose children the scene host's game follows for the others,
    /// each child with the parts of it whose place is followed too. Trobbio's fireworks: the stage shuffles them, puts
    /// each low or high and each shifts its burst at random, so the friend's came in another order and at other heights.
    /// His trapdoor bursts: the stage picks a side, a pattern and a delay for each, so the friend's came from other
    /// trapdoors. (coop) An entry with a rest state counts an object whose first FSM is in that state as not out: the
    /// scene host's game sends only those in action, and the other games switch off theirs at rest, so their own room
    /// FSM's picks never show (each game dropped the Skull King's boulders nearest its own hero, by the room's own Drop
    /// FSM). (coop) The Ant Trapper's barb traps: each barb's own FSM throws it to its rest place moved at random
    /// (TranslateRandom), so the friend's lay 1.1 to 3.2 units from the host's, hurting, for its whole life (NOTES 339).
    /// (coop) The Beastfly's summon cages: each picks where it drops at random (RandomFloat, SetPosition), so the
    /// friend's fell up to 5.8 units from the host's, hurting (NOTES 342).
    /// </summary>
    private static readonly Dictionary<string, (string Container, string[] Parts, string? AtRest)[]> StageFollowed = new() {
        ["Trobbio"] = [("Flare Glitter", ["Explosion", "Pt Antic"], null), ("Trapdoor Bursts/Bursts", [], null)],
        ["Tormented Trobbio"] = [("Flare Glitter", ["Explosion", "Pt Antic"], null), ("Trapdoor Bursts/Bursts", [], null)],
        ["Skull King"] = [("Boulders Battle", [], "Dormant")],
        ["Bone Hunter Trapper"] = [("Barb Traps", [], "Dormant"), ("Barb Traps - Add After Intro", [], "Dormant")],
        ["Bone Flyer Giant"] = [("Summon Cages", [], "Dormant")]
    };

    /// <summary>
    /// (coop) Stage objects beside the enemy (children of its parent) whose names start with one of these, followed as
    /// StageFollowed's are: the Bell Beast's falling bells each pick where they fall and how they bounce at random and
    /// fall by their own physics, so the friend's were up to 16.4 units from the host's. With a rest state as
    /// StageFollowed's.
    /// </summary>
    private static readonly Dictionary<string, (string[] Prefixes, string? AtRest)> StageBeside = new() {
        ["Bone Beast"] = (["BoneBeast DropBell", "BoneBeast AtkBell"], "Dormant")
    };

    /// <summary>
    /// (coop) Whether an enemy of this name has stage objects its game follows, so it gets this component even if it
    /// spawns nothing from the pool.
    /// </summary>
    public static bool HasStage(string name) =>
        StageFollowed.ContainsKey(name) || StageBeside.ContainsKey(name) || StageOwn.ContainsKey(name) ||
        RootFollowed.ContainsKey(name) || FlungThrows.Has(name);

    /// <summary>
    /// (coop) A boss's own objects (its descendants of these names, which its start lets go of) followed as stage
    /// objects: its FSM throws and moves them, and SSMP's copy of the boss runs no FSM, so the other games' copies of
    /// them stayed on the copy or flew by their own physics (the Phantom's pin, apart in 138 of 156 samples; the Last
    /// Judge's censer, up to 9.9 units). In the scene host's game the original's are followed, in the others the copy's;
    /// those the boss's start lets go of (LetGo: the pin, the censer) are let go of on the copy too, the others stay on
    /// it (the censer's slam), so each has the same path as the original's.
    /// </summary>
    private static readonly Dictionary<string, (string Name, bool LetGo)[]> StageOwn = new() {
        ["Phantom"] = [("Pin Projectile", true)],
        ["Last Judge"] = [("Censer Throw", true), ("Censer Slam", false), ("Censer Slam Flame", false)]
    };

    /// <summary>
    /// (coop) Objects at the top of an enemy's room that its FSM throws, by the enemy's name: the FSM variables that hold
    /// them, each with the one that holds the hand it is thrown from. Each is followed itself: its place and FSM states, not its being shown, which its own FSM keeps. SSMP's
    /// copy of the enemy holds one of its own (the Dock Foremen's room has two Projectiles), so the scene host's game
    /// sends the one its enemy holds and the others move the one their copy holds. The Dock Guard Slasher throws his
    /// Projectile and catches it on its way back, and each game flew its own from the copied throw: the friend's flew up
    /// to 5.7 units off the scene host's, or still showed 1.7 s after the host's was caught (a gate round).
    /// </summary>
    private static readonly Dictionary<string, (string Thrown, string Hand)[]> RootFollowed = new() {
        ["Dock Guard Slasher"] = [("Projectile", "Throw Point")]
    };

    /// <summary>
    /// A spawned object the scene host's game follows: its FSMs, and where it was at the last send, its velocity
    /// then, when, and its FSMs' states.
    /// </summary>
    private sealed class Followed {
        public readonly GameObject GameObject;
        public readonly PlayMakerFSM[] Fsms;
        public readonly byte[] States;
        public readonly Object[] Parts;

        /// <summary>(coop) Its hero damagers, its children's included (StageDamage).</summary>
        public readonly DamageHero[] Damagers;
        public Vector2 Position;
        public Vector2 Velocity;
        public float Time;

        /// <summary>(coop) The move between the last two sends, against which a new one turns back (SentSpeed).</summary>
        public Vector2 Move;

        /// <summary>(coop) A boss's own object it carries (not let go of: the Last Judge's Censer Slam): no speed is sent
        /// for it (SentSpeed).</summary>
        public bool Carried;

        /// <summary>
        /// (coop) The entity's FSM, state and action that spawned it (NoState for a stage object), sent with it so a
        /// game that comes into the room later spawns its own copy from the same action.
        /// </summary>
        public (byte Fsm, byte State, byte Action) Spawner = (NoState, NoState, NoState);

        public Followed(GameObject gameObject) {
            GameObject = gameObject;
            Fsms = FsmsOf(gameObject);
            Parts = PartsOf(gameObject);
            Damagers = gameObject.GetComponentsInChildren<DamageHero>(true);
            States = new byte[Fsms.Length];
            Position = gameObject.transform.position;
            Time = UnityEngine.Time.time;
            for (var i = 0; i < States.Length; i++) {
                States[i] = NoState;
            }
        }
    }

    /// <summary>
    /// Another game's copy of a followed object: its FSMs and the scene host's state last given to each.
    /// </summary>
    private sealed class Copy {
        public readonly GameObject GameObject;
        public bool Recovered;
        public readonly PlayMakerFSM[] Fsms;
        public readonly byte[] Given;
        public readonly Object[] Parts;

        /// <summary>(coop) Its hero damagers, in the scene host's object's order (StageDamage).</summary>
        public readonly DamageHero[] Damagers;

        /// <summary>
        /// (coop) Its body, if it has one with a collider of its own that is not a trigger: while that body is dynamic
        /// it hits the ground in the scene host's game, so its path never goes through it.
        /// </summary>
        public readonly Rigidbody2D? SolidBody;

        public Copy(GameObject gameObject) {
            GameObject = gameObject;
            Fsms = FsmsOf(gameObject);
            Parts = PartsOf(gameObject);
            Damagers = gameObject.GetComponentsInChildren<DamageHero>(true);
            if (gameObject.TryGetComponent<Rigidbody2D>(out var body) && System.Array.Exists(
                    gameObject.GetComponentsInChildren<Collider2D>(true), c => c.attachedRigidbody == body && !c.isTrigger)) {
                SolidBody = body;
            }
            Given = new byte[Fsms.Length];
            for (var i = 0; i < Given.Length; i++) {
                Given[i] = NoState;
            }
        }
    }

    /// <summary>
    /// The scene host's last update for a number: place, velocity, when it arrived, and its FSMs' states and floats.
    /// </summary>
    private sealed class Target {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Angle;
        public float Time;

        /// <summary>(coop) The number of the last update that named it (CountsByUpdate).</summary>
        public uint Stamp;
        public readonly byte[] States = new byte[MaxFsms];
        public readonly float[][] Floats = new float[MaxFsms][];
        public int FsmCount;
        public byte[] Parts = [];

        /// <summary>(coop) Each hero damager's amount (StageDamage).</summary>
        public byte[] Damages = [];
        public (byte Fsm, byte State, byte Action) Spawner = (NoState, NoState, NoState);
        public GameObject? Prefab;
        public Vector2 Scale = Vector2.one;
    }

    private readonly NetClient _netClient;

    /// <summary>
    /// Scene host: the followed spawns still out, by number.
    /// </summary>
    private readonly Dictionary<byte, Followed> _followed = new();

    /// <summary>
    /// Other games: each copy by the scene host's number for it.
    /// </summary>
    private readonly Dictionary<byte, Copy> _copies = new();

    /// <summary>
    /// Other games: the scene host's last update for each number.
    /// </summary>
    private readonly Dictionary<byte, Target> _targets = new();

    /// <summary>
    /// Numbers to drop, gathered while going through a dictionary.
    /// </summary>
    private readonly List<byte> _ended = [];

    /// <summary>
    /// The followed stage objects, by number, in this game: whether each is shown or hidden as the scene host's is (a
    /// firework) or only follows its place (its burst), and in the other games whether the scene host's is out.
    /// </summary>
    private readonly Dictionary<byte, (GameObject GameObject, bool Shown, string? AtRest)> _stage = new();

    /// <summary>
    /// (coop) Other games: the object SSMP's copy of the enemy holds that shows a number's stage object, which the other
    /// games move instead: a boss's own (StageOwn), let go of as the original's is or kept on the copy; or one at the top
    /// of its room that the enemy holds (<see cref="RootFollowed"/>), which Waits: it keeps its own place while its FSM
    /// is ahead of the scene host's last update.
    /// </summary>
    private readonly Dictionary<byte, (GameObject GameObject, bool LetGo, bool Waits)> _copySide = new();

    /// <summary>
    /// (coop) Other games: a boss's other objects of an own object's name, kept off (HidesStrays).
    /// </summary>
    private readonly Dictionary<byte, GameObject[]> _strays = new();

    /// <summary>
    /// (coop) Other games: for an object its enemy holds, the hand of SSMP's copy of the enemy it is thrown from.
    /// </summary>
    private readonly Dictionary<byte, GameObject> _hands = new();

    /// <summary>
    /// (coop) Other games: the body type of an object its enemy holds while it is held still (<see cref="Still"/>), to
    /// give back once it moves again.
    /// </summary>
    private readonly Dictionary<byte, RigidbodyType2D> _stillBodies = new();

    private readonly Dictionary<byte, Followed> _stageFollowed = new();
    private readonly Dictionary<byte, Copy> _stageCopies = new();
    private readonly Dictionary<byte, bool> _stageOut = new();
    private readonly Dictionary<byte, bool> _stageSentOut = new();

    /// <summary>
    /// (coop) Other games: the stage objects with a rest state this game's follow switched off (WakesResting).
    /// </summary>
    private readonly HashSet<byte> _rested = [];

    /// <summary>
    /// (coop) Other games: each copy's body given the scene host's speed, with the game time it was placed then; a
    /// physics step <see cref="RunOn.Limit"/> past that stops it (<see cref="OnFixedUpdate"/>).
    /// </summary>
    private readonly Dictionary<Rigidbody2D, float> _carried = new();

    private readonly List<Rigidbody2D> _stopped = [];

    /// <summary>
    /// (coop) Other games: the game time of the latest placement (<see cref="OnLateUpdate"/>); a carried body placed
    /// before it is no longer placed and goes on by itself.
    /// </summary>
    private float _lastPlacing;

    private byte _lastNumber;
    private float _lastSend;

    /// <summary>
    /// (coop) Scene host: the number of its next update (from the clock, so a new scene host's numbers are unrelated
    /// to the last one's). Other games: the number of the last update applied, -1 for none since the scene host came.
    /// </summary>
    private ushort _order = (ushort) System.Environment.TickCount;
    private int _lastOrder = -1;

    /// <summary>
    /// (coop) Other games: a number for each update handled, so an update knows which targets it named (CountsByUpdate).
    /// </summary>
    private uint _stamp;
    private bool _changed;

    /// <summary>(coop) The live components whose enemy's throws fling something followed (FlungThrows).</summary>
    private static readonly List<ThrownComponent> Flinging = [];

    /// <summary>(coop) The pool prefab of each flung name, found once.</summary>
    private static readonly Dictionary<string, GameObject> FlungPrefabs = new();

    /// <summary>(coop) The enemy's name, if its throws fling something followed.</summary>
    private string? _flinger;

    /// <summary>(coop) Other games: the flung throws this game has made its copy of, until the scene host's ends.</summary>
    private readonly HashSet<byte> _flungCopied = [];

    /// <summary>(coop) The throws whose copy here ended itself (switched off, not taken away by its parent).</summary>
    private readonly HashSet<byte> _endedItself = [];

    public ThrownComponent(
        NetClient netClient,
        ushort entityId,
        HostClientPair<GameObject> gameObject
    ) : base(netClient, entityId, gameObject) {
        _netClient = netClient;
        MonoBehaviourUtil.Instance.OnLateUpdateEvent += OnLateUpdate;
        MonoBehaviourUtil.Instance.OnFixedUpdateEvent += OnFixedUpdate;

        var original = gameObject.Host;
        if (original != null && FlungThrows.Has(original.name)) {
            _flinger = original.name;
            Flinging.Add(this);
        }

        var number = FirstStageNumber;
        if (original != null && RootFollowed.TryGetValue(original.name, out var variables) && FollowsRootObjects()) {
            foreach (var (variable, hand) in variables) {
                if (Held(original, variable) is { } thrown && number < NoState - 1) {
                    if (Held(gameObject.Client, variable) is { } held) {
                        _copySide[number] = (held, false, true);
                    }

                    if (Held(gameObject.Client, hand) is { } point) {
                        _hands[number] = point;
                    }

                    _stage[number++] = (thrown, false, null);
                }
            }
        }

        if (original == null || original.transform.parent == null) {
            return;
        }

        foreach (var (container, parts, rest) in StageFollowed.TryGetValue(original.name, out var stageObjects) ? stageObjects : []) {
            if (container.StartsWith("Barb Traps") && !FollowsBarbTraps()) continue;
            if (container == "Summon Cages" && !FollowsSummonCages()) continue;
            var stage = original.transform.parent.Find(container);
            if (stage == null) {
                continue;
            }

            foreach (Transform child in stage) {
                if (!AddStage(child, parts, rest, ref number)) {
                    return;
                }
            }
        }

        foreach (var (name, letGo) in StageOwn.TryGetValue(original.name, out var own) ? own : []) {
            if (OwnObject(original, name) is not { } hostOwn) {
                Logger.Info($"No own object '{name}' of '{original.name}' to follow");
                continue;
            }

            var key = number;
            if (!AddStage(hostOwn.transform, [], null, ref number)) {
                return;
            }

            if (gameObject.Client != null && OwnObject(gameObject.Client, name) is { } copyOwn) {
                _copySide[key] = (copyOwn, letGo, false);
                _strays[key] = System.Array.FindAll(
                    [hostOwn, .. Named(original, name), .. Named(gameObject.Client, name)],
                    other => other != copyOwn);
            }
        }

        if (!StageBeside.TryGetValue(original.name, out var beside)) {
            return;
        }

        foreach (Transform child in original.transform.parent) {
            if (System.Array.Exists(beside.Prefixes, prefix => child.name.StartsWith(prefix)) &&
                !AddStage(child, [], beside.AtRest, ref number)) {
                return;
            }
        }
    }

    /// <summary>
    /// (coop) A boss's own object of that name: what a GameObject variable of that name on one of its FSMs holds (its
    /// start may have let it go already: SSMP's set-up runs the start's actions as it registers the boss), else its
    /// first descendant of that name; or null.
    /// </summary>
    private static GameObject? OwnObject(GameObject root, string name) {
        if (Held(root, name) is { } held && held != null) {
            return held;
        }

        return System.Array.Find(root.GetComponentsInChildren<Transform>(true), t => t != root.transform && t.name == name)
            ?.gameObject;
    }

    /// <summary>
    /// (coop) An object's descendants of that name (not itself).
    /// </summary>
    private static GameObject[] Named(GameObject root, string name) => System.Array.ConvertAll(
        System.Array.FindAll(root.GetComponentsInChildren<Transform>(true), t => t != root.transform && t.name == name),
        t => t.gameObject);

    /// <summary>
    /// Number a stage object and the parts of it whose place is followed too; false once the numbers run out.
    /// </summary>
    private bool AddStage(Transform child, string[] parts, string? rest, ref byte number) {
        if (number > NoState - 1 - parts.Length) {
            return false;
        }

        _stage[number++] = (child.gameObject, true, rest);
        foreach (var part in parts) {
            if (child.Find(part) is { } found) {
                _stage[number++] = (found.gameObject, false, null);
            }
        }

        return true;
    }

    /// <summary>
    /// (coop) Whether an enemy's room objects it throws are followed (<see cref="RootFollowed"/>). Not inlined: the
    /// projectile checks' twins skip it, which gives false.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static bool FollowsRootObjects() => true;

    /// <summary>
    /// (coop) Whether the Ant Trapper's barb traps are followed (StageFollowed). Not inlined: trapper_barbs' twin skips
    /// it, which gives false.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static bool FollowsBarbTraps() => true;

    /// <summary>
    /// (coop) Whether the Beastfly's summon cages are followed (StageFollowed). Not inlined: beastfly_cages' twin skips
    /// it, which gives false.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static bool FollowsSummonCages() => true;

    /// <summary>
    /// (coop) Whether an object its enemy holds is kept from being predicted past the copy that catches it
    /// (<see cref="CatchPoint"/>). Not inlined: projectile_catch's twin skips it, which gives false.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static bool StopsAtCatcher() => true;

    /// <summary>
    /// (coop) Whether an object its enemy holds waits at the hand of SSMP's copy of the enemy while its FSM is ahead of the
    /// scene host's last update. Not inlined: projectile_throw's twin skips it, which gives false.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static bool WaitsAtHand() => true;

    /// <summary>(coop) The object a GameObject variable of that name holds in one of an object's FSMs, if any.</summary>
    private static GameObject? Held(GameObject? owner, string variable) {
        if (owner == null) {
            return null;
        }

        foreach (var fsm in owner.GetComponents<PlayMakerFSM>()) {
            if (fsm.FsmVariables.FindFsmGameObject(variable)?.Value is { } held) {
                return held;
            }
        }

        return null;
    }

    /// <summary>
    /// The FSMs on an object itself (not its children), at most MaxFsms; the same prefab has them in the same order.
    /// </summary>
    private static PlayMakerFSM[] FsmsOf(GameObject gameObject) {
        var fsms = gameObject.GetComponents<PlayMakerFSM>();
        if (fsms.Length > MaxFsms) {
            System.Array.Resize(ref fsms, MaxFsms);
        }

        return fsms;
    }

    /// <summary>
    /// (coop) The parts of an object whose on/off follows the scene host's (PartBits): its children, then its colliders,
    /// then its renderers, its children's included; the same prefab has them in the same order.
    /// </summary>
    private static Object[] PartsOf(GameObject gameObject) => [
        .. System.Array.ConvertAll(gameObject.GetComponentsInChildren<Transform>(true)[1..], t => t.gameObject),
        .. gameObject.GetComponentsInChildren<Collider2D>(true), .. gameObject.GetComponentsInChildren<Renderer>(true)
    ];

    /// <summary>
    /// (coop) Other games: a copy's damagers deal what the scene host's do, after its own FSM has run (StageDamage).
    /// </summary>
    private static void FollowsDamage(Copy copy, byte[] damages) {
        if (!StageDamage.Pairs(damages.Length, copy.Damagers.Length)) {
            return;
        }

        for (var i = 0; i < copy.Damagers.Length; i++) {
            if (copy.Damagers[i] != null && StageDamage.Of(damages, i) is { } damage &&
                copy.Damagers[i].damageDealt != damage) {
                copy.Damagers[i].damageDealt = damage;
            }
        }
    }

    /// <summary>
    /// Whether a part is on.
    /// </summary>
    private static bool IsOn(Object part) => part != null && part switch {
        GameObject child => child.activeSelf,
        Collider2D collider => collider.enabled,
        Renderer renderer => renderer.enabled,
        _ => false
    };

    /// <summary>
    /// (coop) Whether a stage object with a rest state is at rest (its first FSM in that state), so not out.
    /// </summary>
    private static bool Resting(GameObject gameObject, string? rest) =>
        rest != null && gameObject.TryGetComponent<PlayMakerFSM>(out var fsm) && fsm.ActiveStateName == rest;

    /// <summary>
    /// (coop) Other games: a copy's parts on or off as the scene host's are, after its state is given and each frame
    /// after its own FSM has run, so a copy set straight into the host's state shows and hurts as the host's does.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void FollowsParts(Copy copy, byte[] parts) {
        for (var i = 0; i < copy.Parts.Length && PartBits.Carried(i); i++) {
            if (copy.Parts[i] == null) {
                continue;
            }

            switch (copy.Parts[i]) {
                case GameObject child:
                    if (child.activeSelf != PartBits.IsOn(parts, i)) {
                        child.SetActive(PartBits.IsOn(parts, i));
                    }

                    break;
                case Collider2D collider:
                    collider.enabled = PartBits.IsOn(parts, i);
                    break;
                case Renderer renderer:
                    renderer.enabled = PartBits.IsOn(parts, i);
                    break;
            }
        }
    }

    /// <summary>
    /// The index of an FSM's active state, NoState if none.
    /// </summary>
    private static byte StateIndex(PlayMakerFSM fsm) {
        var states = fsm.FsmStates;
        var active = fsm.Fsm.ActiveState;
        for (var i = 0; i < states.Length && i < NoState; i++) {
            if (states[i] == active) {
                return (byte) i;
            }
        }

        return NoState;
    }

    /// <summary>
    /// Scene host: number an object this enemy spawned, if it is one to follow (anything visible, or anything that
    /// can hurt the hero; sounds are not).
    /// </summary>
    /// <param name="spawned">The spawned object.</param>
    /// <param name="fsm">(coop) The index of the entity's FSM whose action spawned it.</param>
    /// <param name="state">(coop) The index of that action's state.</param>
    /// <param name="action">(coop) The index of that action in its state.</param>
    /// <returns>Its number, or 0 if it is not followed.</returns>
    public byte Track(GameObject? spawned, byte fsm = NoState, byte state = NoState, byte action = NoState) {
        if (spawned == null ||
            spawned.GetComponentInChildren<Renderer>(true) == null &&
            spawned.GetComponentInChildren<DamageHero>(true) == null) {
            return 0;
        }

        // A pooled object spawned again is a new throw
        foreach (var (number, followed) in _followed) {
            if (followed.GameObject == spawned) {
                _ended.Add(number);
            }
        }

        foreach (var number in _ended) {
            _followed.Remove(number);
        }

        _ended.Clear();

        // Numbers 1 up to the stage's in turn; 0 is not followed
        _lastNumber = (byte) (_lastNumber % (FirstStageNumber - 1) + 1);
        _followed[_lastNumber] = new Followed(spawned) { Spawner = (fsm, state, action) };
        _changed = true;
        return _lastNumber;
    }

    /// <summary>
    /// Other games: the copy of what the scene host's enemy spawned with that number.
    /// </summary>
    /// <param name="number">The scene host's number for it, 0 if it is not followed.</param>
    /// <param name="copy">This game's copy.</param>
    public void Follow(byte number, GameObject? copy) {
        if (number == 0 || copy == null) {
            return;
        }

        _endedItself.Remove(number); // a new copy of that number: its last one's end is not this one's (the review)

        foreach (var (other, otherCopy) in _copies) {
            if (otherCopy.GameObject == copy) {
                _ended.Add(other);
            }
        }

        foreach (var other in _ended) {
            _copies.Remove(other);
        }

        _ended.Clear();
        var followed = new Copy(copy);
        _copies[number] = followed;
        if (_targets.TryGetValue(number, out var target)) {
            Give(followed, target);
        }
    }

    /// <inheritdoc />
    public override void OnUpdate() {
        if (IsControlled) {
            return;
        }

        foreach (var (number, followed) in _followed) {
            if (followed.GameObject == null || !followed.GameObject.activeInHierarchy) {
                _ended.Add(number);
            }
        }

        foreach (var number in _ended) {
            _followed.Remove(number);
            _changed = true;
        }

        _ended.Clear();

        // A stage object is sent while it is out; one coming out or going away goes out at once
        foreach (var (number, (gameObject, _, rest)) in _stage) {
            var isOut = gameObject != null && gameObject.activeInHierarchy && !Resting(gameObject, rest);
            if (isOut && !_stageFollowed.ContainsKey(number)) {
                _stageFollowed[number] = new Followed(gameObject!) {
                    Carried = _copySide.TryGetValue(number, out var own) && !own.LetGo && !own.Waits
                };
            } else if (!isOut) {
                _stageFollowed.Remove(number);
            }

            if (isOut != (_stageSentOut.TryGetValue(number, out var sentOut) && sentOut)) {
                _changed = true;
            }
        }

        var now = Time.time;

        // What the other games have for each one now, by their prediction from the last update: one off by more than
        // MaxError, or whose FSM has entered another state, goes out at once
        if (!_changed && now - _lastSend >= MinInterval) {
            foreach (var followed in _followed.Values) {
                _changed |= Drifted(followed, now);
            }

            foreach (var followed in _stageFollowed.Values) {
                _changed |= Drifted(followed, now);
            }
        }

        // An update leaves out what has ended, so one goes out at once after a spawn or an end
        if (!_changed && (_followed.Count + _stageFollowed.Count == 0 || now - _lastSend < SendInterval)) {
            return;
        }

        var data = new EntityNetworkData {
            Type = EntityComponentType.Thrown
        };
        data.Packet.Write(_order++);
        data.Packet.Write((byte) (_followed.Count + _stageFollowed.Count));
        foreach (var (number, followed) in _followed) {
            Write(data, number, followed, now);
        }

        foreach (var (number, followed) in _stageFollowed) {
            Write(data, number, followed, now);
        }

        foreach (var number in _stage.Keys) {
            _stageSentOut[number] = _stageFollowed.ContainsKey(number);
        }

        SendData(data);
        _lastSend = now;
        _changed = false;
    }

    /// <summary>
    /// Whether the other games' prediction for a followed object is more than MaxError off now, or one of its FSMs
    /// has entered another state since the last send.
    /// </summary>
    private static bool Drifted(Followed followed, float now) {
        var predicted = followed.Position + followed.Velocity * Mathf.Min(now - followed.Time, MaxPrediction);
        if (Vector2.Distance(predicted, followed.GameObject.transform.position) > MaxError) {
            return true;
        }

        for (var i = 0; i < followed.Fsms.Length; i++) {
            if (followed.Fsms[i] != null && StateIndex(followed.Fsms[i]) != followed.States[i]) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Write a followed object's number, place, velocity and FSMs to an update, and note them as sent.
    /// </summary>
    private static void Write(EntityNetworkData data, byte number, Followed followed, float now) {
        Vector2 position = followed.GameObject.transform.position;
        var elapsed = now - followed.Time;
        // An object moved by its rigidbody reports its velocity; one moved by its FSM or animation, the speed it went
        // at since the last send, (coop) only when that move goes on the way the one before went (SentSpeed)
        var move = position - followed.Position;
        var (speedX, speedY) = SentSpeed.Of(followed.Move.x, followed.Move.y, move.x, move.y, elapsed, followed.Carried);
        var velocity = followed.GameObject.TryGetComponent<Rigidbody2D>(out var body) && body.linearVelocity != Vector2.zero
            ? body.linearVelocity
            : new Vector2(speedX, speedY);
        followed.Move = move;
        followed.Position = position;
        followed.Velocity = velocity;
        followed.Time = now;

        data.Packet.Write(number);
        data.Packet.Write(position.x);
        data.Packet.Write(position.y);
        data.Packet.Write(velocity.x);
        data.Packet.Write(velocity.y);
        // (coop) its turn: a pin thrown at an angle faces along its flight
        data.Packet.Write(followed.GameObject.transform.eulerAngles.z);

        data.Packet.Write((byte) followed.Fsms.Length);
        for (var i = 0; i < followed.Fsms.Length; i++) {
            var fsm = followed.Fsms[i];
            var state = fsm != null ? StateIndex(fsm) : NoState;
            followed.States[i] = state;
            data.Packet.Write(state);

            var floats = fsm != null ? fsm.FsmVariables.FloatVariables : [];
            var count = Mathf.Min(floats.Length, MaxFloats);
            data.Packet.Write((byte) count);
            for (var j = 0; j < count; j++) {
                data.Packet.Write(floats[j].Value);
            }
        }

        var parts = PartBits.Pack(System.Array.ConvertAll(followed.Parts, IsOn));
        data.Packet.Write((byte) parts.Length);
        foreach (var part in parts) {
            data.Packet.Write(part);
        }

        // (coop) how much each of its damagers deals: an FSM can set it on a part it picked at random
        var damages = StageDamage.Pack(System.Array.ConvertAll(followed.Damagers, d => d != null ? d.damageDealt : 0));
        data.Packet.Write((byte) damages.Length);
        foreach (var damage in damages) {
            data.Packet.Write(damage);
        }

        if (number < FirstStageNumber) {
            data.Packet.Write(followed.Spawner.Fsm);
            data.Packet.Write(followed.Spawner.State);
            data.Packet.Write(followed.Spawner.Action);
            // (coop) its scale: a flung bell picks its own size (FlungThrows)
            var scale = followed.GameObject.transform.localScale;
            data.Packet.Write(scale.x);
            data.Packet.Write(scale.y);
        }
    }

    /// <summary>
    /// Other games, each frame after every update: each copy goes where the scene host's is now.
    /// </summary>
    private void OnLateUpdate() {
        if (!IsControlled) {
            return;
        }

        _lastPlacing = Time.time;
        StageFollow();
        if (_copies.Count == 0) {
            return;
        }

        // The scene host's places are about half a round trip old when they arrive
        var lead = _netClient.UpdateManager.AverageRtt / 2000f;
        foreach (var (number, copy) in _copies) {
            if (copy.GameObject == null || !copy.GameObject.activeInHierarchy) {
                if (copy.GameObject != null && !copy.GameObject.activeSelf) _endedItself.Add(number);
                _ended.Add(number);
                continue;
            }

            if (!_targets.TryGetValue(number, out var target)) {
                continue;
            }

            var age = Time.time - target.Time + lead;
            if (age > MaxPrediction) {
                continue;
            }

            FollowsParts(copy, target.Parts);
            FollowsDamage(copy, target.Damages);
            var position = target.Position + target.Velocity * age;
            var transform = copy.GameObject.transform;
            // (coop) a prediction into the ground stops at the scene host's own last place: checked from the copy's place
            // instead, a sickle past the corner of the Vampire Gnat's right wall was refused every frame and flew on by
            // itself, 4 to 7 units off (a gate round, and a probe)
            if (CrossesGround(copy, target.Position, position)) {
                position = target.Position;
            }

            transform.position = new Vector3(position.x, position.y, transform.position.z);
            FollowsTurn(transform, target.Angle);
            if (FlungThrows.IsFlung(target.Spawner.Fsm)) {
                transform.localScale = new Vector3(target.Scale.x, target.Scale.y, transform.localScale.z);
            }
            if (copy.GameObject.TryGetComponent<Rigidbody2D>(out var body)) {
                body.linearVelocity = target.Velocity;
                _carried[body] = Time.time;
            }
        }

        foreach (var number in _ended) {
            _copies.Remove(number);
        }

        _ended.Clear();
    }

    /// <summary>
    /// (coop) Other games, each physics step: a copy whose body has carried it <see cref="RunOn.Limit"/> past its last
    /// placement stops where it is until the next placement (a long frame runs all its physics steps first). One the
    /// latest placement left out is forgotten as it is (<see cref="RunOn.Forgets"/>). A game that is no longer a room's
    /// client forgets them all: its copies are its own objects then.
    /// </summary>
    private void OnFixedUpdate() {
        if (!IsControlled) {
            _carried.Clear();
            return;
        }

        foreach (var (body, placedAt) in _carried) {
            if (body == null || RunOn.Forgets(placedAt, _lastPlacing)) {
                _stopped.Add(body!);
            } else if (RunOn.Stops(Time.fixedTime, placedAt)) {
                body.linearVelocity = Vector2.zero;
                _stopped.Add(body);
            }
        }

        foreach (var body in _stopped) {
            _carried.Remove(body);
        }

        _stopped.Clear();
    }

    /// <summary>
    /// (coop) Whether a solid copy's move would cross the ground: the scene host's places are half a round trip old, and a
    /// falling bomb was predicted on into the floor until the host's bounce arrived (4.6 units down at a 100 ms round
    /// trip).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static bool CrossesGround(Copy copy, Vector2 from, Vector2 to) =>
        copy.SolidBody != null && copy.SolidBody.bodyType == RigidbodyType2D.Dynamic &&
        Physics2D.Linecast(from, to, TerrainMask).collider != null;

    /// <summary>
    /// Other games, each frame after every update: the stage objects show as the scene host's do (a firework the
    /// stage's own FSM set off here is hidden again before it is drawn), and those out go where the host's are.
    /// </summary>
    private void StageFollow() {
        var lead = _netClient.UpdateManager.AverageRtt / 2000f;
        foreach (var (number, (room, shown, _)) in _stage) {
            // (coop) what SSMP's copy of the enemy holds shows it: a boss's own object, let go of as the original's is
            var gameObject = _copySide.TryGetValue(number, out var own) ? own.GameObject : room;
            if (gameObject == null || !_stageOut.TryGetValue(number, out var hostOut)) {
                continue;
            }

            if (!_stageCopies.TryGetValue(number, out var copy)) {
                copy = _stageCopies[number] = new Copy(gameObject);
                if (own.LetGo) {
                    gameObject.transform.SetParent(null, true);
                }
            }

            HidesStrays(number);

            if (shown && gameObject.activeSelf != hostOut) {
                gameObject.SetActive(hostOut);
                if (!hostOut && _stage[number].AtRest != null) {
                    _rested.Add(number);
                } else {
                    _rested.Remove(number);
                }

                // its FSM starts over: the host's state is given afresh
                for (var i = 0; i < copy.Given.Length; i++) {
                    copy.Given[i] = NoState;
                }
            }

            if (!hostOut || !gameObject.activeInHierarchy || !_targets.TryGetValue(number, out var target)) {
                continue;
            }

            // (coop) one its enemy holds, while its FSM is ahead of the scene host's last update (thrown here by the copied
            // throw before the host's word on it came), waits at the copy's hand, still: put where the host's lay, it
            // showed out of place for a frame, and left to fly on its own it went from where it lay through the thrower,
            // 5.3 units behind him in a stall of the host's game (a gate round)
            if (own.Waits && !SameStates(copy, target)) {
                if (_hands.TryGetValue(number, out var hand) && hand != null && WaitsAtHand()) {
                    var at = hand.transform.position;
                    gameObject.transform.position = new Vector3(at.x, at.y, gameObject.transform.position.z);
                    Still(number, gameObject, true);
                }

                continue;
            }

            var age = Mathf.Min(Time.time - target.Time + lead, MaxPrediction);
            var position = target.Position + target.Velocity * age;
            var caught = false;
            if (own.Waits && GameObject.Client != null && StopsAtCatcher()) {
                var x = CatchPoint.Shown(target.Position.x, position.x, GameObject.Client.transform.position.x);
                caught = x != position.x;
                position.x = x;
            }

            var transform = gameObject.transform;
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            // (coop) not one its enemy holds: its own FSM turns it, and given the host's turn the Slasher's Projectile,
            // launched by the copied throw in a long frame, went up to 2.8 units off his hand before it was put back
            if (!own.Waits) {
                FollowsTurn(transform, target.Angle);
            }

            // (coop) one its enemy holds is held still while stopped at its catcher, its body kinematic so its own pull
            // does not carry it off in a long frame (round three's gate rounds: 0.9 units a physics step, 2.4 in a frame of
            // 300 ms), and gets its own body type back once it moves
            if (own.Waits) {
                Still(number, gameObject, caught);
            }

            // (coop) its body moves on as the host's between frames: a bell's own bounce moved its collider off
            if (!caught && gameObject.TryGetComponent<Rigidbody2D>(out var body)) {
                body.linearVelocity = target.Velocity;
                _carried[body] = Time.time;
            }

            FollowsParts(copy, target.Parts);
            FollowsDamage(copy, target.Damages);
        }
    }

    /// <summary>
    /// (coop) Other games: a copy turned as the scene host's is (a pin thrown at an angle: the copy flew level, so its box
    /// was 2 units off in size where its place matched).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void FollowsTurn(Transform transform, float angle) {
        var euler = transform.eulerAngles;
        transform.rotation = Quaternion.Euler(euler.x, euler.y, angle);
    }

    /// <summary>
    /// (coop) Other games: a boss's other objects of a followed own object's name stay off (the original's, which SSMP's
    /// set-up let go of, and any on the copy), so only the followed one shows and hurts (a second censer showed in the
    /// friend's game, off the host's).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void HidesStrays(byte number) {
        if (!_strays.TryGetValue(number, out var strays)) {
            return;
        }

        foreach (var stray in strays) {
            if (stray != null && stray.activeSelf) {
                stray.SetActive(false);
            }
        }
    }

    /// <summary>
    /// (coop) An object its enemy holds, held still: its body kinematic and at rest, so its own pull does not carry it off
    /// before the next placing (2.4 units in a frame of 300 ms); or, moving again, with its own body type back.
    /// </summary>
    private void Still(byte number, GameObject held, bool still) {
        if (!held.TryGetComponent<Rigidbody2D>(out var body)) {
            return;
        }

        if (still) {
            if (!_stillBodies.ContainsKey(number)) {
                _stillBodies[number] = body.bodyType;
                body.bodyType = RigidbodyType2D.Kinematic;
            }

            body.linearVelocity = Vector2.zero;
        } else if (_stillBodies.TryGetValue(number, out var type)) {
            body.bodyType = type;
            _stillBodies.Remove(number);
        }
    }

    /// <summary>
    /// (coop) Whether a copy's FSMs are each in the state the scene host's last update has for them (one it has no state
    /// for counts as the same).
    /// </summary>
    private static bool SameStates(Copy copy, Target target) {
        for (var i = 0; i < copy.Fsms.Length && i < target.FsmCount; i++) {
            if (copy.Fsms[i] != null && target.States[i] != NoState && StateIndex(copy.Fsms[i]) != target.States[i]) {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Other games: give a copy's FSMs the scene host's float variables, and the scene host's state where it entered
    /// another since the last one given (a bomb's fuse runs out with the host's, not its own random one).
    /// </summary>
    private static void Give(Copy copy, Target target) {
        if (copy.GameObject == null || !copy.GameObject.activeInHierarchy) {
            return;
        }

        for (var i = 0; i < copy.Fsms.Length && i < target.FsmCount; i++) {
            var fsm = copy.Fsms[i];
            if (fsm == null) {
                continue;
            }

            var floats = fsm.FsmVariables.FloatVariables;
            var given = target.Floats[i];
            for (var j = 0; j < floats.Length && j < given.Length; j++) {
                floats[j].Value = given[j];
            }

            var state = target.States[i];
            if (state == copy.Given[i] || state >= fsm.FsmStates.Length) {
                continue;
            }

            copy.Given[i] = state;
            if (fsm.Fsm.ActiveState != fsm.FsmStates[state]) {
                fsm.SetState(fsm.FsmStates[state].Name);
            }
        }
    }

    /// <summary>
    /// (coop) Other games, on the first update since this game came into the room: its own copy of a thrown object
    /// already out in the scene host's game, spawned from the copy's same pool action where the scene host's is, then
    /// followed: the Spine Floaters spawn their spines as the room loads in the first player's game, so a player who
    /// came in after saw none on any floater and was never hurt by them.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void CopiesLate(byte number, Target target) {
        if (target.Prefab == null) return;
        Follow(number, target.Prefab.Spawn(target.Position));
        if (_copies.TryGetValue(number, out var copy)) copy.Recovered = true;
    }

    /// <summary>
    /// (coop) Called by the FlingObjectsFromGlobalPool hook for every object flung from the pool, with the object whose FSM
    /// flung it. One of an enemy's throws' (FlungThrows): the scene host's game follows it; the other games put their
    /// own back in the pool at once, since the scene host's shows there instead (CopiesFlung).
    /// </summary>
    internal static void Flung(GameObject? thrower, GameObject? spawned) {
        if (thrower == null || spawned == null) return;
        foreach (var component in Flinging) {
            if (component._flinger == null || !FlungThrows.ThrownBy(component._flinger, thrower.name) ||
                !FlungThrows.IsFlungObject(component._flinger, spawned.name)) continue;
            if (!component.IsControlled) {
                component.Track(spawned, FlungThrows.Marker, 0, 0);
            } else if (FollowsFlung()) {
                spawned.SetActive(false);
            }

            return;
        }
    }

    /// <summary>
    /// (coop) Other games: whether a replayed spawn of this prefab by this object is left out, as the scene host's
    /// spawned ones show here instead (CopiesFlung): the Last Judge's charge bombs.
    /// </summary>
    internal static bool SuppressesFlung(GameObject? thrower, GameObject? prefab) {
        if (thrower == null || prefab == null || !FollowsFlung()) return false;
        foreach (var component in Flinging) {
            if (component._flinger != null && component.IsControlled &&
                FlungThrows.ThrownBy(component._flinger, thrower.name) &&
                FlungThrows.IsFlungObject(component._flinger, prefab.name)) return true;
        }

        return false;
    }

    /// <summary>
    /// (coop) Other games: a copy of what the scene host's throw flung, from this game's pool, then followed.
    /// </summary>
    private void CopiesFlung(byte number, Target target) {
        if (_flinger == null || FlungThrows.Flung(_flinger) is not { } name) return;
        if (!FlungPrefabs.TryGetValue(name, out var prefab) || prefab == null) {
            prefab = null!;
            foreach (var pooled in ObjectPool.instance.pooledObjects.Keys) {
                if (pooled != null && pooled.name == name) {
                    prefab = pooled;
                    break;
                }
            }

            if (prefab == null) return;
            FlungPrefabs[name] = prefab;
        }

        Follow(number, prefab.Spawn(target.Position));
    }

    /// <summary>
    /// (coop) Whether the other games show the scene host's flung objects in place of their own. Not inlined:
    /// widow_bells' twin skips it, which gives false.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static bool FollowsFlung() => true;

    private GameObject? SpawnPrefab(Target target) {
        var (fsm, state, action) = target.Spawner;
        if (GameObject.Client == null || fsm == NoState) return null;

        var fsms = GameObject.Client.GetComponents<PlayMakerFSM>();
        if (fsm >= fsms.Length || state >= fsms[fsm].FsmStates.Length ||
            action >= fsms[fsm].FsmStates[state].Actions.Length ||
            fsms[fsm].FsmStates[state].Actions[action] is not SpawnObjectFromGlobalPool spawn) return null;

        return spawn.gameObject?.Value;
    }

    /// <inheritdoc />
    public override void InitializeHost(uint sceneHostEpoch) {
        WakesResting();
        _copies.Clear();
        _targets.Clear();
        _endedItself.Clear();
        _stageCopies.Clear();
        _stageOut.Clear();
    }

    /// <summary>
    /// (coop) As this game takes the room over, the stage objects its follow switched off at rest as the old scene
    /// host's were come back, so its own room FSM can set them off: a boulder switched off stayed off and never dropped
    /// again. Only those: one off for another reason (a used one, or off as the room loads) stays as it is.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void WakesResting() {
        foreach (var number in _rested) {
            if (_stage.TryGetValue(number, out var stage) && stage.GameObject != null && !stage.GameObject.activeSelf) {
                stage.GameObject.SetActive(true);
            }
        }

        _rested.Clear();
    }

    /// <summary>
    /// (coop) As this game takes the room over, each object SSMP's copy of the enemy held to show the scene host's one
    /// gives what it showed to the room's own, the one the enemy's FSM holds, and takes the room's own rest: the Dock
    /// Guard Slasher's Projectile, out as the host left, flew on forever in the friend's game, never caught, while his
    /// FSM waited for the room's other one; the Last Judge's censer mid-throw hung where the last update had it. A boss's
    /// own object its start lets go of is let go of here, as that start never ran in this game.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal void HandsOver() {
        // (coop) an object its enemy holds that the follow held still gets its own body type back first
        foreach (var (number, type) in _stillBodies) {
            if (_copySide.TryGetValue(number, out var held) && held.GameObject != null &&
                held.GameObject.TryGetComponent<Rigidbody2D>(out var body)) {
                body.bodyType = type;
            }
        }

        _stillBodies.Clear();

        foreach (var number in _stageCopies.Keys) {
            if (!_stage.TryGetValue(number, out var stage) || !_copySide.TryGetValue(number, out var own) ||
                stage.GameObject == null || own.GameObject == null || own.GameObject == stage.GameObject) {
                continue;
            }

            var room = stage.GameObject;
            if (own.LetGo && room.transform.parent != null) {
                room.transform.SetParent(null, true);
            }

            var shown = Look.Of(own.GameObject);
            var rest = Look.Of(room);
            shown.ShowOn(room);
            rest.ShowOn(own.GameObject);
        }
    }

    /// <summary>
    /// (coop) What an object shows, handed over at a take-over (HandsOver): whether it is out, its FSMs' states and
    /// floats, its parts, its place, turn, facing (a Projectile's scale, which its thrower's FSM matches to his) and
    /// speed.
    /// </summary>
    private sealed class Look {
        private bool _out;
        private string[] _states = [];
        private float[][] _floats = [];
        private byte[] _parts = [];
        private Vector3 _position;
        private Quaternion _rotation;
        private Vector3 _scale;
        private Vector2 _velocity;

        public static Look Of(GameObject gameObject) {
            var fsms = FsmsOf(gameObject);
            return new Look {
                _out = gameObject.activeSelf,
                _states = System.Array.ConvertAll(fsms, fsm => fsm.ActiveStateName),
                _floats = System.Array.ConvertAll(fsms,
                    fsm => System.Array.ConvertAll(fsm.FsmVariables.FloatVariables, f => f.Value)),
                _parts = PartBits.Pack(System.Array.ConvertAll(PartsOf(gameObject), IsOn)),
                _position = gameObject.transform.position,
                _rotation = gameObject.transform.rotation,
                _scale = gameObject.transform.localScale,
                _velocity = gameObject.TryGetComponent<Rigidbody2D>(out var body) ? body.linearVelocity : Vector2.zero
            };
        }

        /// <summary>
        /// This look on another object of the same prefab: its states entered after its floats are given, its parts
        /// and speed set after its states' own actions ran.
        /// </summary>
        public void ShowOn(GameObject gameObject) {
            if (gameObject.activeSelf != _out) {
                gameObject.SetActive(_out);
            }

            var copy = new Copy(gameObject);
            for (var i = 0; i < copy.Fsms.Length && i < _states.Length; i++) {
                var floats = copy.Fsms[i].FsmVariables.FloatVariables;
                for (var j = 0; j < floats.Length && j < _floats[i].Length; j++) {
                    floats[j].Value = _floats[i][j];
                }

                if (gameObject.activeInHierarchy && _states[i] is { Length: > 0 } state &&
                    copy.Fsms[i].ActiveStateName != state) {
                    copy.Fsms[i].SetState(state);
                }
            }

            FollowsParts(copy, _parts);
            gameObject.transform.SetPositionAndRotation(_position, _rotation);
            gameObject.transform.localScale = _scale;
            if (gameObject.TryGetComponent<Rigidbody2D>(out var body)) {
                body.linearVelocity = _velocity;
            }
        }
    }

    /// <inheritdoc />
    public override void InitializeClient(uint sceneHostEpoch) {
        _endedItself.Clear(); // a host change: the numbers start over (the short review, 2026-11-05)
        _followed.Clear();
        _stageFollowed.Clear();
        _stageSentOut.Clear();
        _lastOrder = -1;
    }

    /// <inheritdoc />
    public override void Update(EntityNetworkData data, bool alreadyInSceneUpdate) {
        // The server's stored update for a player entering the room is an old one; the next comes within a frame
        if (!IsControlled || alreadyInSceneUpdate) {
            return;
        }

        // A resend of an update already overtaken would put the copies back where they were, in their older states
        var order = data.Packet.ReadUShort();
        if (UpdateOrder.IsStale(order, _lastOrder)) {
            return;
        }

        // (coop) the first update since this game came into the room: what is already out has no copy here yet
        var first = _lastOrder == -1;
        _lastOrder = order;

        var now = Time.time;
        var stamp = ++_stamp;
        var count = data.Packet.ReadByte();
        for (var i = 0; i < count; i++) {
            var number = data.Packet.ReadByte();
            if (!_targets.TryGetValue(number, out var target)) {
                target = new Target();
                _targets[number] = target;
            }

            target.Position = new Vector2(data.Packet.ReadFloat(), data.Packet.ReadFloat());
            target.Velocity = new Vector2(data.Packet.ReadFloat(), data.Packet.ReadFloat());
            target.Angle = data.Packet.ReadFloat();
            target.Time = now;
            target.Stamp = stamp;

            var fsmCount = data.Packet.ReadByte();
            target.FsmCount = Mathf.Min(fsmCount, MaxFsms);
            for (var f = 0; f < fsmCount; f++) {
                var state = data.Packet.ReadByte();
                var floatCount = data.Packet.ReadByte();
                var floats = f < MaxFsms ? target.Floats[f] : null;
                if (f < MaxFsms && (floats == null || floats.Length != floatCount)) {
                    floats = target.Floats[f] = new float[floatCount];
                }

                for (var j = 0; j < floatCount; j++) {
                    var value = data.Packet.ReadFloat();
                    if (floats != null) {
                        floats[j] = value;
                    }
                }

                if (f < MaxFsms) {
                    target.States[f] = state;
                }
            }

            var partBytes = data.Packet.ReadByte();
            if (target.Parts.Length != partBytes) {
                target.Parts = new byte[partBytes];
            }

            for (var j = 0; j < partBytes; j++) {
                target.Parts[j] = data.Packet.ReadByte();
            }

            var damageBytes = data.Packet.ReadByte();
            if (target.Damages.Length != damageBytes) {
                target.Damages = new byte[damageBytes];
            }

            for (var j = 0; j < damageBytes; j++) {
                target.Damages[j] = data.Packet.ReadByte();
            }

            if (number < FirstStageNumber) {
                target.Spawner = (data.Packet.ReadByte(), data.Packet.ReadByte(), data.Packet.ReadByte());
                target.Scale = new Vector2(data.Packet.ReadFloat(), data.Packet.ReadFloat());
                if (FlungThrows.IsFlung(target.Spawner.Fsm)) {
                    if (FlungThrows.NeedsCopy(_copies.ContainsKey(number), _flungCopied.Contains(number)) && FollowsFlung()) {
                        CopiesFlung(number, target);
                        _flungCopied.Add(number);
                    }
                } else {
                // Keep the source while the parent exists; its final death can destroy it before the throw arrives.
                target.Prefab = SpawnPrefab(target) ?? target.Prefab;
                // A dying parent can hide its child throw before the release update arrives.
                if (ThrownCopies.NeedsCopy(first, _copies.ContainsKey(number), _endedItself.Contains(number))) {
                    CopiesLate(number, target);
                }
                }
            }

            if (_copies.TryGetValue(number, out var copy)) {
                Give(copy, target);
            } else if (_stageCopies.TryGetValue(number, out var stageCopy)) {
                Give(stageCopy, target);
            }
        }

        // Every update says which stage objects are out in the scene host's game
        foreach (var number in _stage.Keys) {
            _stageOut[number] = _targets.TryGetValue(number, out var stageTarget) && InUpdate(stageTarget, stamp, now);
        }

        // Each update lists all live throws. A recovered copy's restarted timer must not outlive its native;
        // ordinary copies keep finishing locally.
        foreach (var (number, target) in _targets) {
            if (!InUpdate(target, stamp, now)) {
                _ended.Add(number);
            }
        }

        foreach (var number in _ended) {
            if (_copies.TryGetValue(number, out var copy) && ThrownCopies.StopsAtNativeEnd(copy.Recovered) &&
                copy.GameObject != null) {
                copy.GameObject.SetActive(false);
            }

            _targets.Remove(number);
            _copies.Remove(number);
            _flungCopied.Remove(number);
            _endedItself.Remove(number);
        }

        _ended.Clear();
    }

    /// <summary>
    /// (coop) Whether the update just handled named the target.
    /// </summary>
    private static bool InUpdate(Target target, uint stamp, float now) =>
        CountsByUpdate() ? target.Stamp == stamp : target.Time == now;

    /// <summary>
    /// (coop) Whether an update knows the targets it named by its own number rather than by the frame's time: two
    /// handled in one frame (the scene host's "out" and then "gone") kept the first's out, so the friend's censer, caught
    /// in the host's game, flew on past the Last Judge's hand and stayed there, shown and hurting, until her next throw
    /// (the host sends nothing more once nothing is out). Not inlined: stage_gone's twin skips it, which gives false.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static bool CountsByUpdate() => true;

    /// <inheritdoc />
    public override void Destroy() {
        Flinging.Remove(this);
        MonoBehaviourUtil.Instance.OnLateUpdateEvent -= OnLateUpdate;
        MonoBehaviourUtil.Instance.OnFixedUpdateEvent -= OnFixedUpdate;
    }
}
