using System.Collections.Generic;
using System.Linq;

namespace BossRule;

/// <summary>
/// What happens to players of a boss fight: they watch (one went down while others fight on), come back (the boss
/// was beaten) or die normally (everyone is down, or the last fighter left).
/// </summary>
internal enum OutcomeKind {
    Watch,
    Won,
    Wiped
}

internal record FightOutcome(OutcomeKind Kind, IReadOnlyList<ushort> Players);

/// <summary>
/// What the local player does when a fight ends.
/// </summary>
internal enum ReturnAction {
    None,
    ComeBack,
    DieAfterAll
}

internal static class OutcomeRule {
    /// <summary>
    /// A watcher comes back from a win and dies normally after a wipe, whether or not the outcome names them (a down
    /// that reached the server after the boss died is in no outcome); anyone else does nothing.
    /// </summary>
    public static ReturnAction ForLocal(bool watching, OutcomeKind kind) => !watching ? ReturnAction.None : kind switch {
        OutcomeKind.Won => ReturnAction.ComeBack,
        OutcomeKind.Wiped => ReturnAction.DieAfterAll,
        _ => ReturnAction.None
    };
}

/// <summary>
/// One room's listed boss fight as the server sees it: who still fights, who watches, and how it ends. The server
/// keeps it, so it survives a scene host change. Unity-free.
/// </summary>
internal sealed class FightTracker {
    private readonly HashSet<ushort> _up = new();
    private readonly HashSet<ushort> _watchers = new();

    public bool Active { get; private set; }

    public string Scene { get; private set; } = "";

    public string Flag { get; private set; } = "";

    public IReadOnlyCollection<ushort> Watchers => _watchers;

    /// <summary>
    /// A fight starts with these fighters. False, and nothing changes, while the room's fight is still on: a new scene
    /// host's game must not restart it and drop the watchers.
    /// </summary>
    public bool Start(string scene, string flag, IEnumerable<ushort> fighters) {
        if (Active) return false;

        Scene = scene;
        Flag = flag;
        _up.Clear();
        _watchers.Clear();
        _up.UnionWith(fighters);
        Active = _up.Count > 0;
        return true;
    }

    /// <summary>
    /// A fighter went down: they watch while someone still fights, otherwise everyone who fought dies normally. A
    /// player who was not in the fight (or already watches) gets null: a normal death.
    /// </summary>
    public FightOutcome? Downed(ushort id) {
        if (!Active || !_up.Remove(id)) return null;

        _watchers.Add(id);
        return _up.Count > 0
            ? new FightOutcome(OutcomeKind.Watch, new[] { id })
            : End(OutcomeKind.Wiped, _watchers.ToList());
    }

    /// <summary>
    /// A player left the room or the game. Once nobody fights, the watchers die normally.
    /// </summary>
    public FightOutcome? Left(ushort id) {
        if (!Active) return null;

        _up.Remove(id);
        _watchers.Remove(id);
        if (_up.Count > 0) return null;

        var watchers = _watchers.ToList();
        var end = End(OutcomeKind.Wiped, watchers);
        return watchers.Count > 0 ? end : null;
    }

    /// <summary>
    /// The boss was beaten: the watchers come back.
    /// </summary>
    public FightOutcome? BossDefeated() => Active ? End(OutcomeKind.Won, _watchers.ToList()) : null;

    private FightOutcome End(OutcomeKind kind, IReadOnlyList<ushort> players) {
        Active = false;
        _up.Clear();
        _watchers.Clear();
        return new FightOutcome(kind, players);
    }
}

/// <summary>
/// The server's listed boss fights, one per room. SSMP calls the server add-on on its network thread, and for a player
/// who timed out on that player's send thread, so every call is locked. Unity-free.
/// </summary>
internal sealed class FightRooms {
    private readonly Dictionary<string, FightTracker> _fights = new();

    /// <summary>
    /// A fight starts in a room with these fighters. False, and nothing changes, while that room's fight is on.
    /// </summary>
    public bool Start(string scene, string flag, IEnumerable<ushort> fighters) {
        lock (_fights) {
            if (!_fights.TryGetValue(scene, out var fight)) _fights[scene] = fight = new FightTracker();
            return fight.Start(scene, flag, fighters);
        }
    }

    public FightOutcome? Downed(string scene, ushort id) {
        lock (_fights) return Fight(scene)?.Downed(id);
    }

    public FightOutcome? BossDefeated(string scene) {
        lock (_fights) return Fight(scene)?.BossDefeated();
    }

    /// <summary>
    /// A player left the fights in every room but <paramref name="stillIn"/>: each room whose fight that ends.
    /// </summary>
    public List<(string Scene, FightOutcome Outcome)> Left(ushort id, string? stillIn = null) {
        var ended = new List<(string, FightOutcome)>();
        lock (_fights) {
            foreach (var fight in _fights.Values.Where(f => f.Active && f.Scene != stillIn)) {
                if (fight.Left(id) is { } outcome) ended.Add((fight.Scene, outcome));
            }
        }

        return ended;
    }

    private FightTracker? Fight(string scene) => _fights.TryGetValue(scene, out var fight) && fight.Active ? fight : null;
}
