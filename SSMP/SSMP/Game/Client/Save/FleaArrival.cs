using System;

namespace SSMP.Game.Client.Save;

/// <summary>
/// (coop) A flea another player freed breaks out and flies off here too, as the game alone plays it. The flea's saved
/// value (SavedFlea_ and its room) is shared, but its branches' Control reads it only when the room loads, so the other
/// game's flea stayed caged and wriggling (Bone_06, probe_flea_flyoff, NOTES 362). This part is kept free of Unity
/// types so it can be tested without the game; the hook is in FleaArrival.Hook.cs.
/// </summary>
internal static partial class FleaArrival {
    private const string Prefix = "SavedFlea_";

    /// <summary>The room a flea's saved value belongs to, or null for any other value.</summary>
    public static string? RoomOf(string name) =>
        name.StartsWith(Prefix, StringComparison.Ordinal) && name.Length > Prefix.Length ? name.Substring(Prefix.Length) : null;

    /// <summary>
    /// Whether a flea's value arriving from another player breaks its branches here: it arrived freed, for the room
    /// this player is in, whose branches still hold the flea (their Control in Idle, or in a hit of this player's).
    /// A room loaded with it freed (Activate) or branches already cut are left alone.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static bool Plays(bool freed, string room, string? here, string? state) =>
        freed && room == here && state is ("Idle" or "Hit");
}
