using System.Collections.Generic;

namespace SSMP.Game.Client.Entity.Component;

/// <summary>
/// (coop) In a game that follows the scene host, the copy of an enemy this game's own hit killed dies only when the
/// scene host's enemy did (its Death replay), a round trip later; its death then played again the hit sounds its killing
/// hit had just played (the friend's game: `enemy_damage_old` and `enemy_bone_crack` twice, 56 to 72 ms apart, at
/// Bone_04's Bone Goomba; the enemy page's `extra: enemy_damage_old 1`). The game alone plays the killing hit and its
/// death in one frame, where AudioEventManager plays each clip once. So that death leaves out each clip played since its
/// copy's killing hit here. Unity-free (clips and enemies by instance id), so it is tested without the game.
/// </summary>
internal sealed class KillSounds {
    /// <summary>Seconds a copy's killing hit waits for the scene host's death: far longer than a round trip.</summary>
    public const double Keep = 3.0;

    /// <summary>This game's.</summary>
    public static readonly KillSounds Seen = new();

    private readonly Dictionary<int, double> _played = new();
    private readonly Dictionary<int, double> _killed = new();
    private double? _since;

    /// <summary>A clip this game played (AudioEventManager let it play) at that time.</summary>
    public void Played(int clip, double now) => _played[clip] = now;

    /// <summary>This game's own hit took that copy's health to nothing at that time; it dies on the host's word.</summary>
    public void Killed(int enemy, double now) => _killed[enemy] = now;

    /// <summary>That copy dies now on the scene host's word: from now until <see cref="EndDeath"/>, see Repeats.</summary>
    public void BeginDeath(int enemy, double now) {
        _since = _killed.TryGetValue(enemy, out var hit) && now - hit <= Keep ? hit : null;
        _killed.Remove(enemy);
    }

    public void EndDeath() => _since = null;

    /// <summary>Whether that clip would play again what the dying copy's killing hit here played.</summary>
    public bool Repeats(int clip) => _since is { } since && _played.TryGetValue(clip, out var at) && at >= since;

    public void Clear() {
        _played.Clear();
        _killed.Clear();
        _since = null;
    }
}
