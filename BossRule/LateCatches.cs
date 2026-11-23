using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BossRule;

/// <summary>
/// (coop) What each multi-wounder box of the scene host's game would have done with a catch while it was out, kept
/// KeepS after: a following game's catch can reach the scene host's game after her move's box went there (15 to 34 ms
/// in the 0.3.18 gate, 5 of 19 catches on a busy PC in bosses Task 5's probe), and then the friend was wounded through
/// while she moved on. Unity-free (T: the FSM moves), so it is tested without the game.
/// </summary>
internal sealed class LateCatches<T> {
    /// <summary>How long after a box was last seen out a catch by it is still honoured: the follow's late bar (0.3 s).</summary>
    public const float KeepS = 0.35f;

    private readonly Dictionary<string, (float At, IReadOnlyList<T> Moves)> _seen = new();

    /// <summary>When each box last took its connect here, or had a late catch honoured.</summary>
    private readonly Dictionary<string, float> _took = new();

    /// <summary>The box at that path is out now; a catch by it would make those moves.</summary>
    public void Saw(string path, float now, IReadOnlyList<T> moves) => _seen[path] = (now, moves.ToList());

    /// <summary>
    /// The moves of the named box if it was out within KeepS of now and has not taken its connect since it was out
    /// (here, or by a late catch already honoured: two caught at once restarted her multi slash at the second, the
    /// whole-branch review of 2026-11-05), else null. Honouring it uses it up, unless usesUp is false (the twin's
    /// switch off: every late catch honoured, as before).
    /// </summary>
    /// <remarks>(coop, 0.3.23) graceS: added to KeepS for a catch sent just after its own game froze (FrameGaps.Grace).</remarks>
    public IReadOnlyList<T>? For(string named, float now, bool usesUp = true, float graceS = 0f) {
        foreach (var (path, seen) in _seen) {
            if (!MultiHitPath.Names(path, named) || now - seen.At > KeepS + graceS) continue;
            if (_took.TryGetValue(path, out var took) && took >= seen.At - KeepS) return null;
            if (usesUp) _took[path] = now;
            return seen.Moves;
        }

        return null;
    }

    /// <summary>(coop, 0.4) Seconds since the named box was last seen out here, null if never: printed with each late catch.</summary>
    public float? Since(string named, float now) {
        float? since = null;
        foreach (var (path, seen) in _seen) {
            if (MultiHitPath.Names(path, named) && (since == null || now - seen.At < since)) since = now - seen.At;
        }

        return since;
    }

    /// <summary>The box at that path took its connect in this game.</summary>
    public void Took(string path, float now) => _took[path] = now;

    public void Clear() {
        _seen.Clear();
        _took.Clear();
    }
}
/// <summary>
/// (coop, 0.4 enemies Task 7 Step 2) Each catch tied to its own slash. The scene host's game numbers each multi-wounder
/// box's slashes (each time the box comes out) and sends the number to the room as the box comes out; a following game
/// ties its catch to the number its copy's box came out with, and the scene host's game takes only a catch of the box's
/// latest slash. Without it a catch delayed by seconds (a lag spike, or a test's hold) was honoured on her next slash as
/// its own (NOTES 346: 3.9 s late). Numbers go on across rooms and are judged only by the game that gave them (a new
/// scene host's are its own). Unity-free, so it is tested without the game.
/// </summary>
internal sealed class SlashTies {
    /// <summary>
    /// How long after a slash's number reached a following game a catch by that box still belongs to it. A catch later
    /// than that belongs to the box's next slash, whose number has not come yet: it comes with the box, in the same send
    /// or one behind.
    /// </summary>
    public const float OutS = 1.5f;

    /// <summary>Between a message and its tie, and in a slash's word: in no object's or player's name.</summary>
    public const char Sep = '\u001f';

    /// <summary>The scene host's game: each box's slashes so far.</summary>
    private readonly Dictionary<string, int> _slashes = new();

    /// <summary>The scene host's game: when each box last rose, counted in rises (of two boxes of one name, as an
    /// earlier room's and this room's, the latest risen is the one a catch names).</summary>
    private readonly Dictionary<string, long> _risenAt = new();

    private long _rises;

    /// <summary>A following game: each box's last slash number heard, from which scene host, and when.</summary>
    private readonly Dictionary<string, (string Host, int Slash, float At)> _heard = new();

    /// <summary>The box at that path came out in the scene host's game: its new slash's number.</summary>
    public int Rose(string path) {
        _slashes.TryGetValue(path, out var slash);
        _risenAt[path] = ++_rises;
        return _slashes[path] = slash + 1;
    }

    /// <summary>The word that tells the room the box at that path came out for that slash.</summary>
    public static string Word(string host, string path, int slash) =>
        Sep + slash.ToString(CultureInfo.InvariantCulture) + Sep + host + Sep + path;

    /// <summary>A slash's word read back, or null if the message is none.</summary>
    public static (string Host, string Path, int Slash)? ReadWord(string message) {
        var parts = message.Split(Sep);
        return parts.Length == 4 && parts[0] == "" &&
               int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var slash)
            ? (parts[2], parts[3], slash)
            : null;
    }

    /// <summary>A following game heard the scene host's word for that box's slash.</summary>
    public void Heard(string host, string path, int slash, float now) => _heard[path] = (host, slash, now);

    /// <summary>A following game left the room: the numbers heard there are no more.</summary>
    public void ClearHeard() => _heard.Clear();

    /// <summary>What a following game's catch by the named box carries: its slash and whose number it is, or nothing
    /// when no number for that box was heard.</summary>
    public string Tie(string named, float now) {
        (string Host, int Slash, float At)? last = null;
        foreach (var (path, heard) in _heard) {
            if (MultiHitPath.Names(path, named) && (last == null || heard.At >= last.Value.At)) last = heard;
        }

        if (last is not { } h) return "";
        var slash = now - h.At <= OutS ? h.Slash : h.Slash + 1;
        return Sep + slash.ToString(CultureInfo.InvariantCulture) + Sep + h.Host;
    }

    /// <summary>A catch without its tie, and the tie's scene host (null for none) and slash.</summary>
    public static (string Message, string? Host, int Slash) Untie(string message) {
        var parts = message.Split(Sep);
        return parts.Length == 3 && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var slash)
            ? (parts[0], parts[2], slash)
            : (message, null, 0);
    }

    /// <summary>
    /// The scene host's game: whether a catch tied to that slash by the named box is of the box's latest slash here;
    /// null when it carries no tie, another scene host's numbers, or a box this game never saw out.
    /// </summary>
    public bool? Same(string named, string ownHost, string? host, int slash) {
        if (host == null || host != ownHost) return null;
        return LastRisen(named) is { } path ? slash == _slashes[path] : null;
    }

    /// <summary>The scene host's game: the named box's latest slash here (0 for none yet).</summary>
    public int Latest(string named) => LastRisen(named) is { } path ? _slashes[path] : 0;

    /// <summary>The scene host's game: of the boxes the name names, the one that rose last (null for none).</summary>
    private string? LastRisen(string named) {
        string? last = null;
        var lastAt = 0L;
        foreach (var (path, at) in _risenAt) {
            if (!MultiHitPath.Names(path, named) || at <= lastAt) continue;
            last = path;
            lastAt = at;
        }

        return last;
    }
}
