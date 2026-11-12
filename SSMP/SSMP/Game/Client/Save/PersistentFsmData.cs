using System;
// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace SSMP.Game.Client.Save; 

/// <summary>
/// Data class for a persistent item in the scene with the corresponding FsmInt/FsmBool.
/// </summary>
internal class PersistentFsmData {
    /// <summary>
    /// The persistent item key with the ID and scene name.
    /// </summary>
    public PersistentItemKey PersistentItemKey { get; init; }

    /// <summary>
    /// The game's part the values come from. Once the game destroys it, reading the values throws, so the item is
    /// dropped then.
    /// </summary>
    public UnityEngine.Object? Owner { get; init; }
    
    /// <summary>
    /// Function to get the current integer value. Could be null if a boolean is used instead.
    /// </summary>
    public Func<int> GetCurrentInt { get; init; }
    /// <summary>
    /// Action to set the current integer value. Could be null if a boolean is used instead.
    /// </summary>
    public Action<int> SetCurrentInt { get; init; }
    /// <summary>
    /// Function to get the current boolean value. Could be null if an integer is used instead.
    /// </summary>
    public Func<bool> GetCurrentBool { get; init; }
    /// <summary>
    /// Action to set the current boolean value. Could be null if an integer is used instead.
    /// </summary>
    public Action<bool> SetCurrentBool { get; init; }
    
    /// <summary>
    /// The last value for the integer if used.
    /// </summary>
    public int LastIntValue { get; set; }
    /// <summary>
    /// The last value for the boolean if used.
    /// </summary>
    public bool LastBoolValue { get; set; }

    /// <summary>
    /// A Geo Rock's hits left from the scene host's game, waiting for this game's own hit (<see cref="GeoRockHold"/>).
    /// </summary>
    public GeoRockHold Held { get; } = new();

    /// <summary>(0.3.21) Run each update: a wave arena's held win (ArenaWinHold) loaded once it is due.</summary>
    public System.Action? Tick { get; set; }

    /// <summary>(coop, 0.3.21) Whether the game's item is semi-persistent (<see cref="SemiPersistence"/>).</summary>
    public bool Semi { get; init; }

    /// <summary>(coop, 0.3.21) For an enemy's bool, how many enemies of the room save under its id (0 for others):
    /// a shared id's kill is held to the room's save (<see cref="SemiPersistence.HoldsKill"/>).</summary>
    public int Enemies { get; init; }

    /// <summary>
    /// Whether an int is stored for this data.
    /// </summary>
    public bool IsInt => GetCurrentInt != null;
}
