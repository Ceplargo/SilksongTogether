using System.Collections.Generic;

namespace BossRule;

/// <summary>
/// Which of a room's trap holders rose since the last look: one seen in its risen state after any other state, not one
/// first seen already risen (a game entering the room while it is up did not see it rise). Its waiting state can last
/// less than a frame: the holder the Widow stands on rises as her Charge brings it out, in the physics steps of one long
/// frame, so a look that wanted to see it waiting first missed the rise and the friend's bell never rose (the 0.3.18
/// gate, 2 of 3 rounds, under heavy load). Unity-free, so it is tested without the game.
/// </summary>
internal sealed class RiseWatch {
    private readonly string _waiting;
    private readonly string _risen;
    private readonly Dictionary<string, string> _last = new();

    public RiseWatch(string waiting, string risen) {
        _waiting = waiting;
        _risen = risen;
    }

    /// <summary>A holder's state this frame: whether it rose since the last.</summary>
    /// <param name="quick">a rise after any other state counts (true), or only one seen waiting first (false: the old
    /// rule, GroundBells' twin switch).</param>
    public bool Rose(string holder, string state, bool quick = true) {
        var rose = _last.TryGetValue(holder, out var last) && state == _risen && (quick ? last != _risen : last == _waiting);
        _last[holder] = state;
        return rose;
    }

    /// <summary>A new room: nothing seen yet.</summary>
    public void Clear() => _last.Clear();
}
