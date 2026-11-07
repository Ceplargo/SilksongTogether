using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity;

internal static class EntitySpawnRules {
    internal static string? PrefabName(EntityType type) => type switch {
        EntityType.GrassBall => "Grass Ball",
        EntityType.WoodWasp when SpawnsHiveWasps() => "Shellwood Wasp",
        _ => null
    };

    internal static bool WaitsForSceneRole(EntityType type, bool roleKnown) =>
        !roleKnown && type == EntityType.WoodWasp && SpawnsHiveWasps();

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool SpawnsHiveWasps() => true;
}
