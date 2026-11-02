using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace BossRule;

/// <summary>
/// One Act 1 cutscene of the catalog (cutscenes.json): its name, the rooms it plays in, the object and FSM that run it,
/// the states that start it, whether the games follow each other's (<c>follow</c>, true unless the catalog says false),
/// and the state by which a following game's hero takes input again (<c>release</c>: the cutscene's last, after the state
/// that gives it back; a follow that jumped past that one left the hero held). The catalog's other fields (kind, values,
/// how, probe, note) are the tests' and the list's.
/// </summary>
internal record CutsceneEntry(string Name, IReadOnlyList<string> Scenes, string Object, string Fsm,
    IReadOnlyList<string> Start, bool Follow, string? Release = null, IReadOnlyList<string>? Skip = null,
    string? Cancel = null);

internal static class CutsceneCatalog {
    public static List<CutsceneEntry> Load(string json) =>
        JArray.Parse(json).Select(e => new CutsceneEntry(
            (string) e["name"]!,
            e["scenes"]!.Select(s => (string) s!).ToList(),
            (string) e["object"]!,
            (string) e["fsm"]!,
            e["start"]!.Select(s => (string) s!).ToList(),
            (bool?) e["follow"] ?? true,
            (string?) e["release"],
            e["skip"]?.Select(s => (string) s!).ToList() ?? new List<string>(),
            (string?) e["cancel"])).ToList();

    /// <summary>The entries that play in a room.</summary>
    public static IEnumerable<CutsceneEntry> InRoom(IEnumerable<CutsceneEntry> entries, string scene) =>
        entries.Where(e => e.Scenes.Contains(scene));
}
