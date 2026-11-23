using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace BossRule;

/// <summary>
/// One Act 1 cutscene of the catalog (cutscenes.json): its name, the rooms it plays in, the object and FSM that run it,
/// the states that start it, whether the games follow each other's (<c>follow</c>, true unless the catalog says false),
/// and the state by which a following game's hero takes input again (<c>release</c>: the cutscene's last, after the state
/// that gives it back; a follow that jumped past that one left the hero held). A ride (<c>keeps_hero</c>) is its rider's:
/// a following game keeps its own hero, and one with <c>leave_at</c> plays what its room shows of the states told instead
/// of going to them (their register events and sounds), its object leaving at those states (the caravan's own Disable).
/// A heart (<c>one_reward</c>) gives its reward in the leader's game alone (<see cref="MemoryReward"/>).
/// (0.4 world Task 17 Step 4) A boss's memory (<c>enter</c>): a game whose cutscene FSM is off (SSMP's copy of a boss
/// the scene host runs) goes into the memory itself as the leader's FSM reaches the state that loads it.
/// The catalog's other fields (kind, values, how, probe, note) are the tests' and the list's.
/// </summary>
internal record CutsceneEntry(string Name, IReadOnlyList<string> Scenes, string Object, string Fsm,
    IReadOnlyList<string> Start, bool Follow, string? Release = null, IReadOnlyList<string>? Skip = null,
    string? Cancel = null, bool KeepsHero = false, IReadOnlyList<string>? LeaveAt = null, bool OneReward = false,
    CutsceneEnter? Enter = null);

/// <summary>A memory a game whose cutscene FSM is off enters itself: at the leader's <c>State</c>, that state's own
/// scene change (the Widow's To Memory Scene: BeginSceneTransition to Memory_Needolin by door_wakeOnGround).</summary>
internal record CutsceneEnter(string State, string Scene, string Gate) {
    /// <summary>Whether this game goes into the memory now: its own FSM is off and the leader's reached the state.</summary>
    public static bool Due(CutsceneEnter? enter, string state, bool fsmOn) => !fsmOn && enter?.State == state;
}

internal static class CutsceneCatalog {
    /// <summary>(0.4 world Task 17) The name a cutscene's memory part takes: the entry's name and this.</summary>
    public const string MemoryPart = "/memory";

    public static List<CutsceneEntry> Load(string json) => JArray.Parse(json).SelectMany(e => Parts((JObject) e)).ToList();

    /// <summary>An entry, and its memory part: a memory the cutscene loads (<c>memory</c>: its scene, object, FSM and
    /// start), followed as its own FSM in the memory's room, so the leader's end of it ends every follower's (0.4 world
    /// Task 17: the other game's memory ended only when its own hero touched its orb).</summary>
    private static IEnumerable<CutsceneEntry> Parts(JObject e) {
        var entry = new CutsceneEntry(
            (string) e["name"]!,
            e["scenes"]!.Select(s => (string) s!).ToList(),
            (string) e["object"]!,
            (string) e["fsm"]!,
            e["start"]!.Select(s => (string) s!).ToList(),
            (bool?) e["follow"] ?? true,
            (string?) e["release"],
            e["skip"]?.Select(s => (string) s!).ToList() ?? new List<string>(),
            (string?) e["cancel"],
            (bool?) e["keeps_hero"] ?? false,
            e["leave_at"]?.Select(s => (string) s!).ToList() ?? new List<string>(),
            (bool?) e["one_reward"] ?? false,
            e["enter"] is JObject n ? new CutsceneEnter((string) n["state"]!, (string) n["scene"]!, (string) n["gate"]!) : null);
        yield return entry;
        if (e["memory"] is JObject m) {
            yield return new CutsceneEntry(entry.Name + MemoryPart, new List<string> { (string) m["scene"]! },
                (string) m["object"]!, (string) m["fsm"]!, m["start"]!.Select(s => (string) s!).ToList(), entry.Follow);
        }
    }

    /// <summary>The entries that play in a room.</summary>
    public static IEnumerable<CutsceneEntry> InRoom(IEnumerable<CutsceneEntry> entries, string scene) =>
        entries.Where(e => e.Scenes.Contains(scene));
}
