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

    /// <summary>
    /// (coop, 0.4) A startup spawn held until the scene role is known is registered by the scene host frames after its
    /// FSM ran: a hive wasp's Init had gone on to Idle, and the host sends only what changes after registration, so the
    /// other games kept their own copy's Init. Taken over there, Shellwood_01's, 02's and 26's spawned wasps sat in Init
    /// and never moved (the takeover pass, 2026-11-16; CoopQA snapshots). Its whole FSM, state and variables, is sent
    /// with its next update. Not inlined: takeover_rooms' twin skips it, which gives false.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool SendsHeldSpawnWhole() => true;
}
