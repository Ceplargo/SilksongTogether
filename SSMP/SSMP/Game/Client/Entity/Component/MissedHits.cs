using System;
using System.Collections.Generic;
using System.Linq;

namespace SSMP.Game.Client.Entity.Component;

/// <summary>
/// (coop) In the scene host's game, which of another player's landed hits on an enemy its copied swing missed there.
/// Each landed hit (that game's health change down) is answered by one copied-swing hit on the enemy here within Window
/// either side; one with no answer once Window has passed is due, so the scene host's game plays it (its hit and its
/// stun, as a copied swing's hit). In the new 0.3.14 gate Mossbone Mother's copy trailed her Swoop 2 to 4 units: the
/// friend's swing landed on the copy and its copied swing passed over her, so her stun came 3 hits late. Unity-free, so
/// it is tested without the game.
/// </summary>
internal sealed class MissedHits {
    /// <summary>How far apart (s) a landed hit and a copied-swing hit may come and still be one hit.</summary>
    public const double Window = 0.5;

    private static readonly Dictionary<int, MissedHits> ByEnemy = new();

    private readonly List<(double At, float Stun)> _landed = [];
    private readonly List<double> _shown = [];

    /// <summary>That enemy's record (by its health manager's instance id) in this room.</summary>
    public static MissedHits For(int enemy) {
        if (!ByEnemy.TryGetValue(enemy, out var hits)) {
            hits = new MissedHits();
            ByEnemy[enemy] = hits;
        }

        return hits;
    }

    /// <summary>A new room: no record kept.</summary>
    public static void Clear() => ByEnemy.Clear();

    /// <summary>Another player's game reported its hit on the enemy at that time, with the stun it had there.</summary>
    public void Landed(double at, float stun = 1f) {
        if (!Answer(_shown, at)) _landed.Add((at, stun));
    }

    /// <summary>A copied swing hit the enemy here at that time.</summary>
    public void Shown(double at) {
        var i = _landed.FindIndex(l => System.Math.Abs(l.At - at) <= Window);
        if (i >= 0) {
            _landed.RemoveAt(i);
        } else {
            _shown.Add(at);
        }
    }

    /// <summary>
    /// The stuns of the landed hits no copied swing answered within Window, now past it; each is due once. With atOnce
    /// (copied swings no longer damage the enemy: GamePatcher.OnlyLandedHits) every landed hit is due at once.
    /// </summary>
    public List<float> Due(double now, bool atOnce = false) {
        _shown.RemoveAll(t => now - t > Window);
        var due = _landed.Where(l => atOnce || now - l.At > Window).Select(l => l.Stun).ToList();
        _landed.RemoveAll(l => atOnce || now - l.At > Window);
        return due;
    }

    private static bool Answer(List<double> waiting, double at) {
        var i = waiting.FindIndex(t => System.Math.Abs(t - at) <= Window);
        if (i < 0) return false;
        waiting.RemoveAt(i);
        return true;
    }
}
