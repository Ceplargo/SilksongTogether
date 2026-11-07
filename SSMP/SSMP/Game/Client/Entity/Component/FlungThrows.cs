using System.Collections.Generic;

namespace SSMP.Game.Client.Entity.Component;

/// <summary>
/// (coop) What an enemy's own throws fling in turn, which every game flung on its own: by the enemy's name, the names its
/// throws start with and the name of what they fling. The Widow's vertical slashes each fling a bell up
/// (FlingObjectsFromGlobalPool, at a random speed and angle), her harp shots each move to a random place first, and each
/// bell picks its own size, so the friend's flew up to 7.9 units from the host's, hurting, for up to 2.2 s (NOTES 337).
/// The Last Judge's charge drops a bomb at her Bomb Point, then one every 0.2 s (SpawnObjectFromGlobalPoolOverTime);
/// the other games dropped theirs on their own timer at their copy's point, so under lag a row ran one bomb past the
/// host's or put two in one place (NOTES 339). The scene host's game follows each flung one as
/// a thrown object; the other games put their own back and show the scene host's. Unity-free, so it is tested without
/// the game.
/// </summary>
internal static class FlungThrows {
    /// <summary>The spawner FSM index a flung object is sent with (no FSM of the enemy spawned it).</summary>
    public const byte Marker = 254;

    private static readonly Dictionary<string, (string[] Throwers, string Flung)> Table = new() {
        ["Spinner Boss"] = (["Spinner_vertical_slash", "Spinner BellShot"], "Spinner AtkBell"),
        ["Last Judge"] = (["Last Judge"], "Last Judge Charge Bomb")
    };

    /// <summary>Whether an enemy of this name has throws that fling something followed.</summary>
    public static bool Has(string enemy) => Table.ContainsKey(enemy);

    /// <summary>The name of what this enemy's throws fling (the pool prefab's), or null.</summary>
    public static string? Flung(string enemy) => Table.TryGetValue(enemy, out var entry) ? entry.Flung : null;

    /// <summary>Whether an object of this name (a pooled one ends in "(Clone)") is one of this enemy's throws.</summary>
    public static bool ThrownBy(string enemy, string thrower) =>
        Table.TryGetValue(enemy, out var entry) && System.Array.Exists(entry.Throwers, thrower.StartsWith);

    /// <summary>Whether an object of this name (a pooled one ends in "(Clone)") is what this enemy's throws fling.
    /// </summary>
    public static bool IsFlungObject(string enemy, string name) =>
        Table.TryGetValue(enemy, out var entry) && name.StartsWith(entry.Flung);

    /// <summary>
    /// Whether another game makes a copy of a flung throw from the scene host's update: once per throw, until the scene
    /// host's update ends it. A copy that finished first (the Last Judge's bomb recycled itself before the host's did)
    /// is not made again: put straight into the host's flare, a second one hurt where the host's game had none.
    /// </summary>
    public static bool NeedsCopy(bool hasCopy, bool copiedBefore) => !hasCopy && !copiedBefore;

    /// <summary>Whether a followed object's spawner says it was flung by one of the enemy's throws.</summary>
    public static bool IsFlung(byte spawnerFsm) => spawnerFsm == Marker;
}
