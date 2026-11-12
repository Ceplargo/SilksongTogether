using System.Collections.Generic;

namespace SSMP.Game.Client.Entity;

/// <summary>
/// The register events an object sends once at its start to say it is in the room (the Rock Roller's Broadcast
/// Presence: Sherma at Bone_01's bone gate hides on it). The non-host game's copy has its FSMs off, so it never sent
/// them: the entity sends them in that game when its copy comes out (0.3.21, rock_roller_sherma).
/// </summary>
internal static class CopyPresence {
    private static readonly HashSet<string> Events = ["ROCK ROLLER PRESENT"];

    internal static bool Announces(string? eventName) => eventName != null && Events.Contains(eventName);
}
