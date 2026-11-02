using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BossRule;

/// <summary>
/// One FSM action SSMP copied: sent from this game (it runs the enemy) or applied here (on SSMP's copy of it).
/// </summary>
internal readonly record struct CopiedAction(float Time, string Way, string Object, string Fsm, string State, string Action);

/// <summary>
/// A player in a mark's record: the scene and position are null for another player outside this game's room (SSMP's
/// client API tells only whether they are in it; their own game's record says where they are).
/// </summary>
internal record MarkPlayer(string Name, string? Scene, float? X, float? Y);

/// <summary>
/// An enemy in a mark's record, with each of its FSMs' active state.
/// </summary>
internal record MarkEnemy(string Path, int Hp, float X, float Y, IReadOnlyDictionary<string, string?> Fsms);

/// <summary>
/// The last seconds of copied actions, in a ring of a fixed size, so a long night does not grow it.
/// </summary>
internal class RecentActions {
    private readonly float _seconds;
    private readonly CopiedAction[] _ring;
    private int _next;
    private int _count;

    public RecentActions(float seconds = 10, int size = 4096) {
        _seconds = seconds;
        _ring = new CopiedAction[size];
    }

    public void Add(float time, string way, string obj, string fsm, string state, string action) {
        _ring[_next] = new CopiedAction(time, way, obj, fsm, state, action);
        _next = (_next + 1) % _ring.Length;
        _count = Math.Min(_count + 1, _ring.Length);
    }

    /// <summary>
    /// The actions of the last seconds before <paramref name="now"/>, oldest first.
    /// </summary>
    public List<CopiedAction> Since(float now) =>
        Enumerable.Range(0, _count)
            .Select(i => _ring[(_next - _count + i + _ring.Length) % _ring.Length])
            .Where(a => now - a.Time <= _seconds)
            .ToList();
}

/// <summary>
/// A mark's id and the JSON record each game writes for it (BossRule\marks\&lt;id&gt;\&lt;player&gt;.json).
/// </summary>
/// <summary>
/// How many marks a source may still have written or forwarded this session: a mark comes from another player's game,
/// so a game that sends them on and on must not fill this PC's disk (each is a screenshot), nor the others'.
/// </summary>
internal sealed class MarkBudget {
    public const int Max = 50;
    private readonly Dictionary<ushort, int> _used = new();

    /// <summary>Whether one more from this source fits (counted if so); source 0 for a game's received marks.</summary>
    public bool Take(ushort source = 0) {
        _used.TryGetValue(source, out var used);
        if (used >= Max) return false;
        _used[source] = used + 1;
        return true;
    }

    /// <summary>A source's count back to none (a player who left).</summary>
    public void Forget(ushort source) => _used.Remove(source);
}

internal static class MarkRecord {
    private const int MaxLength = 64;

    /// <summary>
    /// The mark's id: this PC's time and the presser, only ASCII letters, digits, - and _ (a folder name).
    /// </summary>
    public static string Id(DateTime time, string presser) {
        var name = Safe(presser);
        return $"{time.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture)}-{(name.Length > 0 ? name : "player")}";
    }

    /// <summary>
    /// The text with only ASCII letters, digits, - and _ kept, at most 64 of them: an id or a name from another player's
    /// game is untrusted, and becomes a folder or a file name here.
    /// </summary>
    public static string Safe(string text) {
        var kept = new string(text.Where(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_').ToArray());
        return kept.Length > MaxLength ? kept.Substring(0, MaxLength) : kept;
    }

    /// <summary>
    /// The record: <paramref name="now"/> is the mark's moment on the copied actions' clock, so each says how long before
    /// the mark it was copied (ago).
    /// </summary>
    public static string Json(string mark, string by, string player, DateTimeOffset time, string? scene,
        IEnumerable<MarkPlayer> players, IEnumerable<MarkEnemy> enemies, IEnumerable<CopiedAction> copied, float now) {
        var record = new JObject {
            ["mark"] = mark,
            ["by"] = by,
            ["player"] = player,
            ["time"] = time.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", CultureInfo.InvariantCulture),
            ["scene"] = scene,
            ["players"] = new JArray(players.Select(p => new JObject {
                ["name"] = p.Name, ["scene"] = p.Scene, ["x"] = p.X, ["y"] = p.Y
            })),
            ["enemies"] = new JArray(enemies.Select(e => new JObject {
                ["path"] = e.Path, ["hp"] = e.Hp, ["x"] = e.X, ["y"] = e.Y,
                ["fsms"] = new JObject(e.Fsms.Select(f => new JProperty(f.Key, f.Value)))
            })),
            ["copied"] = new JArray(copied.Select(a => new JObject {
                ["ago"] = Math.Round(now - a.Time, 3), ["way"] = a.Way, ["object"] = a.Object, ["fsm"] = a.Fsm,
                ["state"] = a.State, ["action"] = a.Action
            }))
        };
        return record.ToString(Formatting.Indented);
    }
}
