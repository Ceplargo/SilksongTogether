using System;
using System.Linq;

namespace BossRule;

/// <summary>
/// (0.3.20) The room objects a player's own game sets off (its hero stands on, lands on, hits or walks under them) and
/// that change the room's terrain or hurt, and how each is set off in another game. Unity-free, so BossRule.Tests
/// compiles it.
/// </summary>
internal static class LocalTraps {
    /// <summary>Each kind: its FSM's name, how its object's name starts, the state it waits in, and each state its setting
    /// off enters with the event that enters it from the waiting state.</summary>
    public static readonly (string Fsm, string Name, string[] Waits, (string State, string Event)[] Sets)[] Kinds = [
        // a hero lands on it (COLLIDE, its checks) or hits it (DAMAGED): it crumbles, breaks and rebuilds
        ("bone_crumble_plat", "bone_plat_", ["Idle", "Check Collision", "Check Collision 2", "Try H Crumble"],
            [("Crumble Antic", "CRUMBLE")]),
        // (0.3.21) the same platform's FSM under another name: Bone_03's bone_plat_02_crumble (probe_touch_sources)
        ("Control", "bone_plat_02_crumble", ["Idle", "Check Collision", "Check Collision 2", "Try H Crumble"],
            [("Crumble Antic", "CRUMBLE")]),
        // a hero lands on it (its trigger's LAND) or slashes its chain: it falls and stays down
        ("Control", "Chain Drop Platform", ["Idle"], [("Break Antic", "LAND"), ("Slash Break", "BREAK")]),
        // a floor that collapses under a hero (Bone_01's Battle Scene); its Antic Type passes inside one frame, so a
        // watch sees Idle, then Antic or Antic Metal (0.3.21's probe_collapser)
        ("Control", "Collapser Small", ["Idle"], [("Antic Type", "BREAK"), ("Antic", "BREAK"), ("Antic Metal", "BREAK")]),
        // a boulder a hero's walk or hit drops: it falls and hurts (Impact Damager 2)
        ("Control", "Bone_Boulder", ["Idle", "Shake"], [("Drop Antic", "DROP")]),
        // a wall a hero strikes rumbles and explodes (Bone_07's Explode Sequence)
        ("Control", "explode_wall", ["Idle"], [("Strike", "STRIKE"), ("Set PD", "STRIKE S")]),
        // a deposit each hit wears down until it breaks
        ("Control", "tool_metal_deposit", ["Idle"], [("Hit Register Event", "HIT"), ("Instant Break", "BREAK")]),
        // (0.3.21) a jail door three hits break (Bone_12): its Break lets its Jail Scene's prisoner out. Only the break
        // is told: a hit's Hit Pos Check cancels by the local hero's side, so another game's count could differ
        ("Control", "jail_door", ["Idle", "Hit"], [("Break", "BREAK")]),
        // (0.3.21) Bone_10's Caravan Lech, wounded when its own hero comes back by the Spa Door: its Wounded State puts
        // the Lech away and its Wounded child out. No event leads there from Idle: "@" tells the state, set directly
        ("Dialogue", "Caravan Lech", ["Idle"], [("Wounded State", "@Wounded State")]),
    ];

    /// <summary>(0.3.21) The word a state-told kind ("@") going off in its set state tells the room: the Lech's Wounded
    /// State puts the Lech away in the frame it enters. Null for terrain (a scene unloading takes it off broken).</summary>
    public static string? SeenSet(string fsm, string name, string? now) => Kind(fsm, name) is { } kind
        ? kind.Sets.Where(s => s.State == now && StateOf(s.Event) != null).Select(s => s.Event).FirstOrDefault()
        : null;

    /// <summary>(0.3.21 Task 10 Step 2) A set state that keeps a game static for some room changes, told to every
    /// player (not only the room's) so one who comes in later sees what the doer's game shows: the Lech's Wounded State
    /// sets LECH_WOUNDED for 3 (its SetStaticVariable), which its Wounded? reads first on each entry.</summary>
    private static readonly (string Name, string Event, string Static, int Rooms)[] Lasting =
        [("Caravan Lech", "@Wounded State", "LECH_WOUNDED", 3)];

    /// <summary>TrapFollow's room word's prefix ("Trap:" path "|" event).</summary>
    public const string TrapPrefix = "Trap:";

    /// <summary>The static and its room changes a room word's (path "|" event) set state keeps, or null.</summary>
    public static (string Static, int Rooms)? LastingOf(string word) {
        var bar = word.LastIndexOf('|');
        if (bar < 0) return null;
        var path = word.Substring(0, bar);
        var (name, ev) = (path.Substring(path.LastIndexOf('/') + 1), word.Substring(bar + 1));
        return Lasting.Where(l => name.StartsWith(l.Name, StringComparison.Ordinal) && l.Event == ev)
            .Select(l => ((string, int)?) (l.Static, l.Rooms)).FirstOrDefault();
    }

    /// <summary>Whether the server sends a word to every other player, not only the room's: a room word whose set
    /// state keeps a static (one word: a second sent in the same frame replaced the first, single data).</summary>
    public static bool ToEveryone(string state) => state.StartsWith(TrapPrefix, StringComparison.Ordinal) &&
                                                   LastingOf(state.Substring(TrapPrefix.Length)) != null;

    /// <summary>(0.3.21) The state a room word names ("@" and the state), set directly; null for an event.</summary>
    public static string? StateOf(string ev) => ev.StartsWith("@", StringComparison.Ordinal) ? ev.Substring(1) : null;

    /// <summary>(0.3.21) A player's hazard respawn sends that game's broken ones back whole at once: each kind's FSM,
    /// how its name starts, its event, the states seen just after a reset (its reset state passes inside one frame),
    /// the states only a reset leaves for those, and every state a reset leaves from.</summary>
    public static readonly (string Fsm, string Name, string Event, string[] Into, string[] Leaves, string[] From)[]
        Resets = [
            ("bone_crumble_plat", "bone_plat_", "HAZARD RESPAWNED", ["Hazard Respawn Reset", "Idle"],
                ["Crumble Antic", "Break", "Core Antic"], ["Crumble Antic", "Break", "Core Antic", "Rebuild"]),
            // the same FSM under another name: Bone_03's bone_plat_02_crumble (probe_traps falling, 2026-11-10)
            ("Control", "bone_plat_02_crumble", "HAZARD RESPAWNED", ["Hazard Respawn Reset", "Idle"],
                ["Crumble Antic", "Break", "Core Antic"], ["Crumble Antic", "Break", "Core Antic", "Rebuild"]),
        ];

    /// <summary>(0.3.21) A crumbling platform's timed states and the state each one's own wait leads to. The game whose
    /// hero set it off tells each step ("@" and the next state); another game's platform waits for it, because a hit's
    /// or a respawn's pause stops the setting-off game's clock (0.17 s each), so its timers ran behind and the other game
    /// played a Core Antic or Rebuild the setting-off game never did (probe_traps fall, 2026-11-10_174448).</summary>
    public static readonly (string Fsm, string Name, (string From, string To)[] Steps)[] Steps = [
        ("bone_crumble_plat", "bone_plat_", [("Break", "Core Antic"), ("Core Antic", "Rebuild")]),
        ("Control", "bone_plat_02_crumble", [("Break", "Core Antic"), ("Core Antic", "Rebuild")]),
    ];

    /// <summary>(0.3.21) How long past its own wait another game's platform waits for the step before taking it itself
    /// (the setting-off game left, or its word was lost).</summary>
    public const float StepWaitS = 1.0f;

    /// <summary>(0.3.21) The step word the setting-off game sends as its platform goes from one timed state to the next,
    /// or null.</summary>
    public static string? StepOf(string fsm, string name, string? before, string? now) =>
        StepsOf(fsm, name).Where(s => s.From == before && s.To == now).Select(s => "@" + s.To).FirstOrDefault();

    /// <summary>(0.3.21) The state a step word leads out of, or null for any other word.</summary>
    public static string? StepFrom(string fsm, string name, string ev) =>
        StepsOf(fsm, name).Where(s => "@" + s.To == ev).Select(s => s.From).FirstOrDefault();

    /// <summary>(0.3.21) The state a timed state's own wait leads to, or null if it is not a held one.</summary>
    public static string? NextOf(string fsm, string name, string? state) =>
        StepsOf(fsm, name).Where(s => s.From == state).Select(s => s.To).FirstOrDefault();

    /// <summary>(0.3.21) Whether another game's platform still waits for the step: in it <paramref name="since"/>
    /// seconds, its own wait <paramref name="wait"/>.</summary>
    public static bool HoldsStep(float since, float wait) => since < wait + StepWaitS;

    /// <summary>(0.3.21) Whether this game tells a platform's steps: its own hero set it off, and if another game's
    /// did too (both heroes on it at once, each game's set-off before the other's word), only the room's scene host's
    /// does, so exactly one game tells them and the other follows.</summary>
    public static bool TellsSteps(bool ownSetOff, bool otherSetOff, bool sceneHost) =>
        ownSetOff && (!otherSetOff || sceneHost);

    private static (string From, string To)[] StepsOf(string fsm, string name) => Steps
        .Where(k => k.Fsm == fsm && name.StartsWith(k.Name, StringComparison.Ordinal)).Select(k => k.Steps)
        .FirstOrDefault() ?? [];

    /// <summary>The Skull King's: they drop on his attack, which his fight's own follow sends (StageFollowed).</summary>
    private const string BossBoulders = "Bone_Boulder SK";

    private static (string Fsm, string Name, string[] Waits, (string State, string Event)[] Sets)? Kind(string fsm,
        string name) => name.StartsWith(BossBoulders, StringComparison.Ordinal) ? null : Kinds
        .Where(k => k.Fsm == fsm && name.StartsWith(k.Name, StringComparison.Ordinal))
        .Cast<(string, string, string[], (string, string)[])?>().FirstOrDefault();

    /// <summary>The event this game's own setting off of such an object sends the room (it just entered a set-off state
    /// from another), or null.</summary>
    public static string? Sets(string fsm, string name, string? before, string? now) {
        if (Kind(fsm, name) is not { } kind || before == now) return null;
        if (Reset(fsm, name) is { } reset && before != null && now != null && reset.Leaves.Contains(before) &&
            reset.Into.Contains(now)) return reset.Event;
        if (kind.Sets.Any(s => s.State == before)) return null;
        return kind.Sets.Where(s => s.State == now).Select(s => s.Event).FirstOrDefault();
    }

    /// <summary>Whether another game's word sets off this game's object: only one still waiting.</summary>
    public static bool Echoes(string fsm, string name, string? now, string ev) =>
        now != null && (Resets.Any(r => r.Event == ev)
            ? Reset(fsm, name) is { } reset && reset.Event == ev && reset.From.Contains(now)
            : Kind(fsm, name) is { } kind && kind.Waits.Contains(now));

    private static (string Fsm, string Name, string Event, string[] Into, string[] Leaves, string[] From)? Reset(
        string fsm, string name) => Resets.Where(r => r.Fsm == fsm && name.StartsWith(r.Name, StringComparison.Ordinal))
        .Cast<(string, string, string, string[], string[], string[])?>().FirstOrDefault();
}
