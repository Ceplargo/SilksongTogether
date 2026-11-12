using System.Collections.Generic;

namespace BossRule;

/// <summary>
/// (0.3.21, the project's ruling) The flea caravan's arrival is the rider's respawn: its stop's "Death Respawn
/// Marker" sits on the caravan's arrival door (door_caravanTravelEnd) and leaves with the caravan. The caravan's stop is
/// shared (CaravanTroupeLocation) and a player's respawn is their own, so after another player rides it on, a player
/// whose respawn is on the old stop's marker has a respawn with no marker: their next death or restart never gets back
/// into play (HeroController.Respawn finds no spawn point). The game alone never leaves a player behind, as the rider
/// arrives with the caravan. That player's respawn goes to the caravan's new stop, as the rider's did.
/// </summary>
internal static class CaravanRule {
    public const string Marker = "Death Respawn Marker";

    /// <summary>The respawn scene a player's respawn moves to, or null when it stays: it is on a caravan stop's arrival
    /// marker (one of <paramref name="stops"/>) and the caravan is at another stop now.</summary>
    public static string? Moved(string? respawnScene, string? respawnMarker, string? stop, ICollection<string> stops) =>
        respawnMarker == Marker && stop != null && respawnScene != null && respawnScene != stop && stops.Contains(respawnScene)
            ? stop
            : null;
}
