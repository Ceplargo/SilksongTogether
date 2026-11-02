namespace BossRule;

/// <summary>What a following game does with one of its wave arenas (ArenaWins).</summary>
internal enum ArenaStep { None, End, Open }

/// <summary>
/// (coop) The step for an ordinary wave arena in a game that does not run its room. Unity-free, so it is tested without
/// the game.
/// </summary>
internal static class ArenaEnd {
    /// <summary>
    /// End (the game's own end, as the scene host's had: its gates' BG OPEN and the rest) once the arena's win came and it
    /// has not ended here, started or not: a walk-in after the win then starts nothing. Open once the shared save
    /// completed it while it ran (SSMP's sync of its saved object, whose BattleCompleted sends only the BG QUICK OPEN some
    /// gates do not take, and no end). Else nothing, so a later wave is never cut short.
    /// </summary>
    public static ArenaStep For(bool started, bool completed, bool won) =>
        completed ? started ? ArenaStep.Open : ArenaStep.None : won ? ArenaStep.End : ArenaStep.None;
}
