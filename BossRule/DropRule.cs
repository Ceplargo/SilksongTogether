using System.Collections.Generic;

namespace BossRule;

/// <summary>
/// (0.3.21) Which player's take of an enemy's item drop counts (the server's side of DropShare). Each game
/// drops its own copy of the item when the enemy dies (PersistentEnemyItemDrop), and one kill gives one item in the game
/// alone. The first take of a drop the server hears of counts; a later one, from a game that took its own copy within
/// the network delay, is given back there.
/// </summary>
internal sealed class DropTakes {
    private readonly HashSet<string> _taken = [];

    /// <summary>Whether this take of the drop <paramref name="id"/> in <paramref name="scene"/> is its first.</summary>
    public bool Take(string scene, string id) => _taken.Add(scene + "/" + id);
}
