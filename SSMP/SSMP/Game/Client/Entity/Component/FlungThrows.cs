using System;
using System.Collections.Generic;
using System.Linq;

namespace SSMP.Game.Client.Entity.Component;

/// <summary>
/// (coop) What an enemy's own throws fling in turn, which every game flung on its own: by the enemy's name, the names its
/// throws start with and the name of what they fling. The Widow's vertical slashes each fling a bell up
/// (FlingObjectsFromGlobalPool, at a random speed and angle), her harp shots each move to a random place first, and each
/// bell picks its own size, so the friend's flew up to 7.9 units from the host's, hurting, for up to 2.2 s (NOTES 337).
/// The Last Judge's charge drops a bomb at her Bomb Point, then one every 0.2 s (SpawnObjectFromGlobalPoolOverTime);
/// the other games dropped theirs on their own timer at their copy's point, so under lag a row ran one bomb past the
/// host's or put two in one place (NOTES 339). A Rock Roller's corpse throws 3 bombs as it explodes (Control /
/// Explode: SpawnObjectFromGlobalPool, then SetVelocity2d at a random speed), each game its own: one that landed
/// at a ledge's edge rolled off in the friend's game and blew up 1 s later 5 units away, the host's explosion
/// missing there (7 of 65 recorded Rock Roller rows; 0.3.21 Task 12). The scene host's game follows each flung one as
/// a thrown object; the other games put their own back and show the scene host's. Unity-free, so it is tested without
/// the game.
/// </summary>
internal static class FlungThrows {
    /// <summary>The spawner FSM index a flung object is sent with (no FSM of the enemy spawned it).</summary>
    public const byte Marker = 254;

    private static readonly Dictionary<string, (string[] Throwers, string Flung)> Table = new() {
        ["Spinner Boss"] = (["Spinner_vertical_slash", "Spinner BellShot"], "Spinner AtkBell"),
        ["Last Judge"] = (["Last Judge"], "Last Judge Charge Bomb"),
        ["Rock Roller"] = (["Corpse Rock Roller"], "Rock Roller Bomb")
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

    /// <summary>The spawner state a flung pickup is sent with (a throw's is 0).</summary>
    public const byte PickupKind = 1;

    /// <summary>
    /// (coop, 0.3.21) The pickups an enemy's death flings (FlingObject at a random speed and angle), each game its own: a
    /// Rock Roller's corpse leaves the Rock Rollers wish's item (Control / Spawn Pickup: CreateObject "Collectable Item
    /// Pickup Instant", named "Rock Roller_item_Rock Rollers", then FlingObject), and the two games' landed up to 11 units
    /// apart, once on the floor below (wish_message; the project 2026-11-10 19:23: one pickup for both players,
    /// falling and landing the same way on both screens). By the enemy's name, the names that fling them and the start
    /// of the pickup's name.
    /// </summary>
    private static readonly Dictionary<string, (string[] Throwers, string Prefix)> Pickups = new() {
        ["Rock Roller"] = (["Corpse Rock Roller"], "Rock Roller_item_")
    };

    /// <summary>Whether a flung object is one of this enemy's death pickups, flung by one of its throwers.</summary>
    public static bool IsPickup(string enemy, string thrower, string name) =>
        Pickups.TryGetValue(enemy, out var entry) && name.StartsWith(entry.Prefix) &&
        System.Array.Exists(entry.Throwers, thrower.StartsWith);

    /// <summary>Whether a followed object's spawner state says it is a flung pickup.</summary>
    public static bool IsPickupKind(byte spawnerState) => spawnerState == PickupKind;

    /// <summary>
    /// (coop, 0.3.21) The spawner state a followed enemy item drop is sent with (HealthManager.SpawnItemDrop); its action
    /// byte is the drop's index in the enemy's drop table (DropIndex).
    /// </summary>
    public const byte DropKind = 2;

    /// <summary>Whether a followed object's spawner state says it is an enemy's item drop.</summary>
    public static bool IsDropKind(byte spawnerState) => spawnerState == DropKind;

    /// <summary>
    /// (coop, 0.3.21) Whether this enemy's HealthManager item drops are followed: the scene host's roll decides, the other
    /// games show its item (the project 2026-11-10 20:31: The Marrow's Pilgrims drop the Pilgrim Rags wish's Pilgrim
    /// Rag, a shared count; rag_drop's red, 220353-pair2: the host's rag alone). shortcut: by name, The Marrow's Pilgrims;
    /// add an enemy here when another wish item drops from one.
    /// </summary>
    public static bool DropsFollowed(string enemy) => enemy.StartsWith("Pilgrim");

    /// <summary>
    /// (coop, 0.3.21) Whether a fling (FlingUtils.FlingObject) goes out as a followed drop: only an item drop's, inside
    /// HealthManager.SpawnItemDrop, with its index in the drop table. Money (rosaries, shell shards: SpawnCurrency)
    /// stays each game's own (the owner: money may lie differently).
    /// </summary>
    public static bool SendsFling(bool inItemDrop, byte? dropIndex) => inItemDrop && dropIndex != null;

    /// <summary>A drop's index in a drop table (its groups' drops one after another), or null (none, or past a byte).</summary>
    public static byte? DropIndex<T>(IEnumerable<IEnumerable<T>> groups, Func<T, bool> isIt) =>
        groups.SelectMany(g => g).Select((d, i) => (d, i)).Where(p => isIt(p.d) && p.i < byte.MaxValue)
            .Select(p => (byte?) p.i).FirstOrDefault();

    /// <summary>The drop at that index in a drop table (DropIndex), or the default.</summary>
    public static T? DropAt<T>(IEnumerable<IEnumerable<T>> groups, byte index) =>
        groups.SelectMany(g => g).Skip(index).FirstOrDefault();
}

/// <summary>
/// (coop, 0.3.21) Pairs the other games' own flung pickups, held back unflung, with the scene host's numbered ones, in the
/// order each comes: either may come first (the scene host's update a round trip late, or this game's corpse late).
/// </summary>
internal sealed class PickupPairs<T> where T : class {
    private readonly Queue<T> _own = new();
    private readonly Queue<byte> _host = new();

    /// <summary>This game's own pickup came: the scene host's number it now follows, or null until one comes.</summary>
    public byte? Own(T own) {
        if (_host.Count > 0) return _host.Dequeue();
        _own.Enqueue(own);
        return null;
    }

    /// <summary>The scene host's pickup came: this game's own pickup to follow it, or null until one comes.</summary>
    public T? Host(byte number) {
        if (_own.Count > 0) return _own.Dequeue();
        _host.Enqueue(number);
        return null;
    }

    /// <summary>Forgets both: this game's own pickups no host's came for, given back to show as its own.</summary>
    public List<T> Clear() {
        var unpaired = new List<T>(_own);
        _own.Clear();
        _host.Clear();
        return unpaired;
    }
}
