using System.Collections.Generic;
using System.Linq;

namespace SSMP.Game.Client;

/// <summary>
/// (coop, 0.3.21) A copy's bone segment release, held a moment (RemoteTouches): the copy's box, read each physics step,
/// left a segment for a step and came back (Bone_03's left4 bridge, probe_bridge_walk 234751-pair2: Segment 6 released
/// at 2.49 s and pressed again at 2.53 s, a second dust the hero's own game, in contact throughout, never made). A
/// segment the copy left is released only once it has stayed off HOLD; coming back before that keeps it pressed with no
/// new press. Unity-free, so it is tested without the game.
/// </summary>
internal sealed class BoneHolds {
    /// <summary>How long a left segment waits before its release.</summary>
    public const double Hold = 0.1;

    private readonly Dictionary<int, double> _left = new();

    /// <summary>The copy's box left this segment now.</summary>
    public void Left(int id, double now) => _left[id] = now;

    /// <summary>The copy's box is on this segment again: whether it was waiting (then no new press).</summary>
    public bool Back(int id) => _left.Remove(id);

    /// <summary>The segments whose hold ran out by now: release them.</summary>
    public List<int> Due(double now) {
        var due = _left.Where(p => now - p.Value >= Hold).Select(p => p.Key).ToList();
        foreach (var id in due) _left.Remove(id);
        return due;
    }

    /// <summary>Every segment still waiting (the copy gone: release them all).</summary>
    public List<int> All() {
        var all = _left.Keys.ToList();
        _left.Clear();
        return all;
    }
}
