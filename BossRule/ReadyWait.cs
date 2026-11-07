using System.Collections.Generic;
using System.Linq;

namespace BossRule;

/// <summary>
/// (coop) Which players' games have a room's boss (they said so: BossReady), and whether a held fight may start: every
/// other player in the arena has it, or <see cref="TimeoutS"/> seconds passed since all were in. A slow PC loads a boss
/// scene after its hero takes control (Greymoor_05_boss 5.6 s after the room, NOTES 302), so a start then left that
/// game without the boss's first throws (0.3.18 gate: Moorwing 7.9 s late).
/// </summary>
internal sealed class ReadyWait {
    /// <summary>
    /// Seconds a fight waits for a game that lacks the boss, after which it starts anyway with today's catch-up: past the
    /// 0.35 to 3.37 s the gates measured, short of a wait that feels stuck.
    /// </summary>
    public const float TimeoutS = 5f;

    private readonly HashSet<(ushort Id, string Scene)> _ready = new();
    private float? _since;

    public void Ready(ushort id, string scene) => _ready.Add((id, scene));

    public void Forget(ushort id) => _ready.RemoveWhere(r => r.Id == id);

    public void Clear() {
        _ready.Clear();
        _since = null;
    }

    /// <summary>A new hold: the timeout counts again from the next time all are in.</summary>
    public void Restart() => _since = null;

    public List<ushort> Missing(string scene, IEnumerable<ushort> inArea) =>
        inArea.Where(id => !_ready.Contains((id, scene))).ToList();

    /// <summary>
    /// Whether the held start may go now: the players in the arena (not this game's) all have the boss, or the timeout ran
    /// out since this hold first found one without it.
    /// </summary>
    public bool MayGo(string scene, IEnumerable<ushort> inArea, float now) {
        if (Missing(scene, inArea).Count == 0) return true;
        _since ??= now;
        return now - _since.Value >= TimeoutS;
    }
}
