using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity;

/// <summary>
/// (coop) Which scene loaded beside the active one is a part of its room, whose entities register with the room's own
/// (a boss or boss-defeated scene its loader adds: Bone_East_08_boss_golem to Bone_East_08). The rule was the name alone:
/// one that starts with the room's name. A move's target room loads beside the room it leaves too, before it becomes the
/// active one, and some carry the old room's name: Shellwood_11b_Memory (Shellwood_11b's deep memory), Belltown_Room_Spare,
/// Bone_East_10_Church. Registered with the old room's host role, an entity of the new room kept the old role's hiding after
/// its game became the new room's scene host: the Flower Queen stayed off in her memory, a co-op soft-lock (0.4 world Task
/// 17 Step 4, deep-twin3-p3; the game alone fine, flower-alonemem2-p3). So the move's target is no part; it registers
/// when it becomes the active scene. Unity-free.
/// </summary>
internal static class RoomParts {
    /// <summary>
    /// Whether the scene named loaded is a part of the active room: its name starts with the room's, it is not the room,
    /// and it is not the move's target (moveTarget: the room a scene move under way goes to, or null).
    /// </summary>
    public static bool IsPart(string loaded, string active, string? moveTarget) =>
        loaded.StartsWith(active) && loaded != active && !(SkipsMoveTarget() && loaded == moveTarget);

    /// <summary>
    /// Whether a move's target room is kept out. Not inlined: room_parts' twin switches it off with a patch (it then
    /// reads false), and the target's entities register with the old room's role again.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool SkipsMoveTarget() => true;
}
