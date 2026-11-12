using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Save;

/// <summary>
/// (0.3.21) A saved bool whose object the game destroyed in the frame its value changed (Bone_04's Pilgrim 03 (1),
/// killed by the other player's hit: its death arrives late in the frame, the game saves it and destroys the enemy
/// before the next compare): the change is read from its stored value as the object is dropped, so it still goes out.
/// Without it the other game never got the death, and the enemy stood again there (probe_pilgrim03).
/// </summary>
internal static class DroppedChange {
    /// <summary>The value to send for a dropped bool: its stored one if it differs from the last compared, else none.</summary>
    public static bool? Of(bool hasStored, bool stored, bool lastCompared) =>
        hasStored && stored != lastCompared ? stored : null;

    /// <summary>The switch a check's twin skips (a skipped bool method returns false): no dropped change goes out.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool Sends() => true;
}
