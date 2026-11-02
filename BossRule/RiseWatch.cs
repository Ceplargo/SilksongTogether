using System.Collections.Generic;

namespace BossRule;

/// <summary>
/// Which of a room's trap holders rose since the last look: one that went from its waiting state to its risen state, not
/// one first seen already risen (a game entering the room while it is up did not see it rise). Unity-free, so it is
/// tested without the game.
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
    public bool Rose(string holder, string state) {
        var rose = _last.TryGetValue(holder, out var last) && last == _waiting && state == _risen;
        _last[holder] = state;
        return rose;
    }

    /// <summary>A new room: nothing seen yet.</summary>
    public void Clear() => _last.Clear();
}
