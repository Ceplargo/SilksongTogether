using System.Collections.Generic;
using System.Linq;

namespace SSMP.Game.Client.Entity.Component;

/// <summary>
/// (coop) Which enemies each copied swing has stunned: a copied swing (another player's attack shown in this game)
/// stuns an enemy at most once, however many of its first hits land on it. In the combined 0.3.15 gate the friend's
/// copied swing hit Mossbone Mother twice in one frame in the scene host's game (both from DamageEnemies' damage
/// buffer, their health put back as a copied swing's is): her stun counted two and she staggered one friend hit early.
/// Unity-free, so it is tested without the game.
/// </summary>
internal sealed class RemoteStuns {
    /// <summary>Seconds a swing's stuns are kept: a swing is over well within this.</summary>
    public const double Keep = 2.0;

    /// <summary>This game's.</summary>
    public static readonly RemoteStuns Seen = new();

    private readonly Dictionary<(int Enemy, int Swing), double> _stunned = new();

    /// <summary>Whether this swing may stun this enemy now: the first time only, while it is kept.</summary>
    public bool First(int enemy, int swing, double now) {
        foreach (var key in _stunned.Where(p => now - p.Value > Keep).Select(p => p.Key).ToList()) {
            _stunned.Remove(key);
        }

        if (_stunned.ContainsKey((enemy, swing))) return false;
        _stunned[(enemy, swing)] = now;
        return true;
    }

    public void Clear() => _stunned.Clear();
}
