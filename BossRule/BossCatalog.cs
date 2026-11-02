using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace BossRule;

/// <summary>
/// How a boss fight starts: an FSM that waits in <c>State</c> until <c>Event</c> (kind "fsm"), or a BattleScene
/// (kind "battleScene").
/// </summary>
internal record BossStart(string Kind, string? Object, string? Fsm, string? State, string? Event) {
    /// <summary>
    /// Whether this fight's FSM, in <paramref name="state"/>, starts on <paramref name="fsmEvent"/>: each is one of the
    /// '|'-separated parts of <c>State</c> and <c>Event</c>.
    /// </summary>
    public bool StartsOn(string state, string fsmEvent) => Has(State, state) && Has(Event, fsmEvent);

    /// <summary>
    /// Whether a '|'-separated list holds the item as one of its parts, without allocating: StartGate.ArmOnStart asks
    /// for every FSM event until a gate is armed in a boss room.
    /// </summary>
    private static bool Has(string? list, string item) {
        list ??= "";
        for (var at = 0;; at++) {
            var end = list.IndexOf('|', at);
            if (end < 0) end = list.Length;
            if (end - at == item.Length && string.CompareOrdinal(list, at, item, 0, item.Length) == 0) return true;
            if (end == list.Length) return false;
            at = end;
        }
    }
}

/// <summary>
/// Where players count as in the arena: inside a trigger object's collider (kind "trigger"), or anywhere in the room
/// (kind "room").
/// </summary>
internal record BossArea(string Kind, string? Object);

/// <summary>
/// A number the boss compares with its health to change phase, with its value at the boss's base health: an FSM
/// variable (<c>Var</c>), or a fixed number in an action, the first of type <c>Action</c> in <c>State</c>, its field
/// <c>Field</c> (Fourth Chorus's HP Check states compare with 451, 326 and 201 written in the action).
/// </summary>
internal record BossPhase(string Object, string Fsm, string? Var, float Value, string? State = null, string? Action = null,
    string? Field = null);

/// <summary>
/// For an arena whose intro runs in the scene host's game only: once the scene host's start FSM is in <c>State</c>,
/// another game's start FSM still in one of <c>From</c> goes there too. <c>Echo</c>: states of the scene host's intro whose
/// actions the other games run on their own arena as the scene host's enters each (the Last Judge's gate pilgrims sing
/// from her arena's NPCs Start Singing, which in their games never comes).
/// </summary>
internal record BossFollow(string State, string From, IReadOnlyList<string> Echo) {
    /// <summary>
    /// The echo state to send as the scene host's arena moves from left to entered: an entry "A" when it enters A, an
    /// entry "A>B" when it goes from A to B, so A's own flow ran through it (the Last Judge's NPCs Start Singing cancels
    /// at once while her bells are not all rung, and then the host's pilgrims stay silent); else none.
    /// </summary>
    public string? EchoOn(string? left, string entered) =>
        Echo.Select(e => e.Split('>'))
            .FirstOrDefault(parts => parts.Length == 1 ? parts[0] == entered : parts[0] == left && parts[1] == entered)?[0];

    /// <summary>Whether a state is one of the echo states (an entry's part before any '>').</summary>
    public bool Echoes(string state) => Echo.Any(e => e.Split('>')[0] == state);
}

/// <summary>
/// One boss of the catalog (bosses.json): the PlayerData flag set when it is beaten, its room, how its fight starts,
/// its arena, the flags a fresh game needs for it to appear, its base health (what its room gives each body at load;
/// <c>BodyHp</c> for a body with its own) and its phase thresholds; <c>Onward</c>: what of its room its own start shuts and
/// a held fight shuts too, each with the doors it is onward from (StartGate.ShutsOnward); <c>Release</c>: its start leaves
/// the hero held until the boss's own FSM gives it back, so a game that does not run it gives its hero back once the fight
/// is on (StartGate.Release).
/// </summary>
internal record BossEntry(
    string Flag,
    string Scene,
    string Gate,
    string Boss,
    BossStart Start,
    BossArea Area,
    IReadOnlyDictionary<string, bool> Needs,
    int Hp,
    IReadOnlyList<BossPhase> Phases,
    IReadOnlyDictionary<string, int>? BodyHp = null,
    BossFollow? Follow = null,
    IReadOnlyList<BossOnward>? Onward = null,
    BossEnd? End = null,
    bool Release = false
) {
    /// <summary>
    /// A body's base health, which the boss's health settings multiply.
    /// </summary>
    public int BaseHp(string body) => BodyHp != null && BodyHp.TryGetValue(body, out var hp) ? hp : Hp;
}

/// <summary>
/// (coop) What of a room its fight's start shuts that a held fight shuts too (its path's end), and the doors from which
/// it is the way onward (StartGate.ShutsOnward).
/// </summary>
internal record BossOnward(string Object, IReadOnlyList<string> From);

/// <summary>
/// (coop) A room that ends on its own FSM's state, not a wave arena's (the Widow's Boss Scene): the FSM (its path), the
/// state the room ends in, and the states before it from which a game that did not run the end follows there on the win
/// (RoomEnd).
/// </summary>
internal record BossEnd(string Object, string Fsm, string State, IReadOnlyList<string> From);

/// <summary>
/// Reads the boss catalog. Unity-free.
/// </summary>
internal static class BossCatalog {
    /// <exception cref="FormatException">An entry misses a value BossRule needs.</exception>
    public static IReadOnlyList<BossEntry> Load(string json) => JArray.Parse(json).Select(Entry).ToList();

    /// <summary>
    /// The entries a fight in this room could be for, best first: flag still false and every need met; more needs first
    /// (a variant with conditions of its own, such as a black-threaded one, before the plain fight), then catalog order.
    /// </summary>
    public static IReadOnlyList<BossEntry> Candidates(IEnumerable<BossEntry> entries, string scene, Func<string, bool> playerData) =>
        entries.Where(e => e.Scene == scene && !playerData(e.Flag) && e.Needs.All(n => playerData(n.Key) == n.Value))
            .OrderByDescending(e => e.Needs.Count).ToList();

    private static BossEntry Entry(JToken e) {
        var flag = (string?) e["flag"] ?? "?";
        JToken Need(string key) => e[key] ?? throw new FormatException($"{flag}: missing {key}");

        var start = Need("start");
        var area = Need("area");
        return new BossEntry(
            flag,
            (string) Need("scene")!,
            (string) Need("gate")!,
            (string) Need("boss")!,
            new BossStart((string) start["kind"]!, (string?) start["object"], (string?) start["fsm"],
                (string?) start["state"], (string?) start["event"]),
            new BossArea((string) area["kind"]!, (string?) area["object"]),
            (e["needs"] as JObject)?.Properties().ToDictionary(p => p.Name, p => (bool) p.Value)
            ?? new Dictionary<string, bool>(),
            (int) Need("hp"),
            (e["phases"] as JArray)?.Select(p =>
                new BossPhase((string) p["object"]!, (string) p["fsm"]!, (string?) p["var"], (float) p["value"]!,
                    (string?) p["state"], (string?) p["action"], (string?) p["field"])
            ).ToList() ?? new List<BossPhase>(),
            (e["bodyHp"] as JObject)?.Properties().ToDictionary(p => p.Name, p => (int) p.Value),
            e["follow"] is JObject follow
                ? new BossFollow((string) follow["state"]!, (string) follow["from"]!,
                    ((string?) follow["echo"])?.Split('|') ?? [])
                : null,
            (e["onward"] as JArray)?.Select(o => new BossOnward((string) o["object"]!,
                (o["from"] as JArray)?.Select(d => (string) d!).ToList() ?? new List<string>())).ToList()
            ?? new List<BossOnward>(),
            e["end"] is JObject end
                ? new BossEnd((string) end["object"]!, (string) end["fsm"]!, (string) end["state"]!,
                    ((string) end["from"]!).Split('|'))
                : null,
            (bool?) e["release"] ?? false
        );
    }
}
