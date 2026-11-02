using System.Collections.Generic;
using System.Linq;

namespace BossRule;

/// <summary>
/// One cutscene FSM of this game's room as the games follow each other's (cutscenes.json): whether it has started
/// (entered one of its start states, on its own or by a follow), whether it leads (it started on its own: its player set
/// the cutscene off), the states it entered since, and whether a state another game's FSM entered moves this one there.
/// Each game runs its own cutscene on its own hero; a follow only keeps them in step past what waits for one player (a
/// talk's press, a bind, a trigger only that player's hero touched). One game leads a run: had a follower told its own
/// states too, a state it skipped by a follow would have sent the leader back (the Silkspear shrine's dialogue twice).
/// </summary>
internal sealed class CutsceneRun {
    private readonly IReadOnlyCollection<string> _starts;
    private readonly HashSet<string> _entered = new();
    private ushort _seen;

    public CutsceneRun(IEnumerable<string> starts) => _starts = starts.ToList();

    public bool Started { get; private set; }

    /// <summary>Whether this game's run started on its own (its player set the cutscene off): it leads, others follow it.</summary>
    public bool Leads { get; private set; }

    /// <summary>How many states this run told the other games: the last one's number.</summary>
    public ushort Sent { get; private set; }

    /// <summary>
    /// This game's FSM entered a state, on its own or by a follow (<paramref name="followed"/>): whether the other games
    /// in the room are told, which they are of each state a leading run's FSM entered first and on its own.
    /// </summary>
    public bool Entered(string state, bool followed) {
        if (!Started && _starts.Contains(state)) {
            Started = true;
            Leads = !followed;
        }

        if (!(Started && _entered.Add(state) && !followed && Leads)) return false;
        Sent++;
        return true;
    }

    /// <summary>
    /// Another game's FSM entered a state, the <paramref name="seq"/>-th its leader told: whether this game's FSM, now in
    /// <paramref name="current"/>, goes there. A leading run follows nobody; before its start only to a start (never into
    /// the middle); once started by a follow to each state it has not entered, so it never starts twice or goes back to a
    /// state it passed. A state told before one already seen (a resend that came in late) moves nothing.
    /// </summary>
    public bool Follows(ushort seq, string state, string current) {
        if (seq <= _seen) return false;
        _seen = seq;
        return !Leads && state != current && (Started ? !_entered.Contains(state) : _starts.Contains(state));
    }
}
