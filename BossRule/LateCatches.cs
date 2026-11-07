using System.Collections.Generic;
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
    public IReadOnlyList<T>? For(string named, float now, bool usesUp = true) {
        foreach (var (path, seen) in _seen) {
            if (!MultiHitPath.Names(path, named) || now - seen.At > KeepS) continue;
            if (_took.TryGetValue(path, out var took) && took >= seen.At - KeepS) return null;
            if (usesUp) _took[path] = now;
            return seen.Moves;
        }

        return null;
    }

    /// <summary>The box at that path took its connect in this game.</summary>
    public void Took(string path, float now) => _took[path] = now;

    public void Clear() {
        _seen.Clear();
        _took.Clear();
    }
}
