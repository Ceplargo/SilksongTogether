namespace BossRule;

/// <summary>
/// (coop) Whether a game starts its own ordinary wave arena because another game in the room started it (ArenaStarts;
/// the owner, 2026-11-20: an ordinary arena starts on every screen at once). Unity-free, so it is tested without the game.
/// </summary>
internal static class ArenaStart {
    /// <summary>A follow message's state that is an arena's start: "Arena Started: " and the arena's path.</summary>
    public const string Prefix = "Arena Started: ";

    /// <summary>
    /// Follow another game's start: this game is in that room, the arena is an ordinary one (the boss catalog's starts
    /// are StartGate's), and here it has neither started nor been won.
    /// </summary>
    public static bool Follows(bool sameRoom, bool catalogRoom, bool started, bool completed) =>
        sameRoom && !catalogRoom && !started && !completed;
}
