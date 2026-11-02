namespace SSMP.Game.Server;

/// <summary>
/// When the host's game saves itself while friends play: friends' progress reached disk only when the host's game
/// saved (a bench), so a crash lost theirs too (upstream review #5). The host's game saves with the game's own save,
/// as quitting does, and that writes the friends' save data in the same moment, so after a crash the world and
/// everyone's things come back in step. Kept free of Unity types so it can be tested without the game.
/// </summary>
internal static class CoopAutoSave {
    /// <summary>
    /// Seconds between saves while friends are in (checks set it shorter).
    /// </summary>
    public static float IntervalSeconds = 180;

    /// <summary>
    /// Whether the host's game should save now.
    /// </summary>
    /// <param name="now">The time in seconds.</param>
    /// <param name="lastSave">When the host's game last saved (any save: a bench too).</param>
    /// <param name="friends">How many other players are in.</param>
    /// <param name="friendLeft">Whether a friend left since the last save.</param>
    /// <param name="inControl">Whether the host's hero is in the game and under the player's control: not dead,
    /// watching a fight, in a cutscene, between rooms or paused.</param>
    public static bool Due(double now, double lastSave, int friends, bool friendLeft, bool inControl) =>
        inControl && (friendLeft || friends > 0 && now - lastSave >= IntervalSeconds);
}
