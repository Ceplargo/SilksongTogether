using System.Collections.Generic;
using System.Linq;

namespace BossRule;

/// <summary>
/// The scene host's rises of a room's trap holders that came while this game's holder was not out and waiting, each
/// kept a moment and applied when it is: when the Widow's Charge brings her ground bell holders out, the one she stands
/// on rises at once in the scene host's game, and that rise can reach a following game before SSMP's copy of her Charge
/// brings its holders out (the gate 2026-11-01_113002: 0.015 and 0.029 s before; dropped, the holder never rose there).
/// Unity-free, so it is tested without the game.
/// </summary>
internal sealed class EarlyRises {
    private readonly float _keepS;
    private readonly Dictionary<string, float> _came = new();

    /// <param name="keepS">How long a rise is kept for its holder to come out, in seconds.</param>
    public EarlyRises(float keepS) => _keepS = keepS;

    /// <summary>A rise of that holder came at now (seconds) while it was not out and waiting.</summary>
    public void Came(string holder, float now) => _came[holder] = now;

    /// <summary>That holder is out and waiting at now: whether a rise of it came within the keep time before. Once.
    /// </summary>
    public bool Due(string holder, float now) {
        if (!_came.TryGetValue(holder, out var came)) return false;
        _came.Remove(holder);
        return now - came <= _keepS;
    }

    /// <summary>The holders whose kept rise is older than the keep time at now, each told once and dropped.</summary>
    public List<string> Expired(float now) {
        var old = _came.Where(c => now - c.Value > _keepS).Select(c => c.Key).ToList();
        foreach (var holder in old) _came.Remove(holder);
        return old;
    }

    /// <summary>A new room: nothing kept.</summary>
    public void Clear() => _came.Clear();
}
