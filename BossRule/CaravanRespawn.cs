using System;
using System.Linq;
using System.Runtime.CompilerServices;
using GlobalEnums;
using UnityEngine;

namespace BossRule;

/// <summary>Each frame while connected: <see cref="CaravanRule"/> on this player's own respawn.</summary>
internal static class CaravanRespawn {
    // the caravan's stops (CaravanLocationScenes: Bone_10, Greymoor_08, Coral_Judge_Arena, Aqueduct_05)
    private static readonly string[] Stops = Enum.GetValues(typeof(CaravanTroupeLocations)).Cast<CaravanTroupeLocations>()
        .Select(CaravanLocationScenes.GetSceneName).Where(s => s != null).ToArray();

    /// <summary>Whether a left-behind respawn follows the caravan. Not inlined: caravan_respawn's twin skips it.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool Follows() => true;

    public static void Tick() {
        var pd = PlayerData.instance;
        if (pd == null) return;
        var stop = CaravanLocationScenes.GetSceneName(pd.CaravanTroupeLocation);
        if (CaravanRule.Moved(pd.respawnScene, pd.respawnMarkerName, stop, Stops) is not { } moved || !Follows()) return;
        Debug.Log($"[BossRule] The caravan went on to {moved}: this player's respawn on it at {pd.respawnScene} follows");
        pd.respawnScene = moved;
    }
}
