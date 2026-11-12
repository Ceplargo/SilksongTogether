using HarmonyLib;

namespace SSMP.Game.Client.Save;

/// <summary>(0.3.21) A wave arena's private started and completed flags (BattleScene), for its arriving win.</summary>
internal static class ArenaState {
    private static readonly AccessTools.FieldRef<BattleScene, bool> StartedRef =
        AccessTools.FieldRefAccess<BattleScene, bool>("started");

    private static readonly AccessTools.FieldRef<BattleScene, bool> CompletedRef =
        AccessTools.FieldRefAccess<BattleScene, bool>("completed");

    /// <summary>Whether the arena started its battle here (its first wave came, its end not yet).</summary>
    public static bool Started(BattleScene battle) => StartedRef(battle);

    /// <summary>Whether the arena is won here (its own end running or over).</summary>
    public static bool Done(BattleScene battle) => CompletedRef(battle);
}
