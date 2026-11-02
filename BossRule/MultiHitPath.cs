namespace BossRule;

/// <summary>
/// How a game names the multi-hitter that caught its hero to the scene host's game, and how that game finds its own:
/// the box's path below its boss with the boss's name as the scene host's game has it (SSMP's copy of the Widow is the
/// root object "Spinner Boss(Clone)", her own ".../Boss Scene/Spinner Boss"). Unity-free, so it is tested without the game.
/// </summary>
internal static class MultiHitPath {
    private const string Clone = "(Clone)";

    /// <summary>Whether the box at that path is on SSMP's copy of a boss (a root object named "...(Clone)").</summary>
    public static bool IsCopy(string path) {
        var slash = path.IndexOf('/');
        return slash > 0 && path.Substring(0, slash).EndsWith(Clone);
    }

    /// <summary>The copy's box at that path as the scene host's game names its own: the root without "(Clone)".</summary>
    public static string Of(string copyPath) {
        var slash = copyPath.IndexOf('/');
        var root = slash < 0 ? copyPath : copyPath.Substring(0, slash);
        if (root.EndsWith(Clone)) root = root.Substring(0, root.Length - Clone.Length);
        return slash < 0 ? root : root + copyPath.Substring(slash);
    }

    /// <summary>Whether the box at that path is the one named.</summary>
    public static bool Names(string path, string named) => path == named || path.EndsWith("/" + named);

    /// <summary>The message for that player's hero caught by the named box (a box's path has no bar, a name may).</summary>
    public static string Message(string player, string named) => player + "|" + named;

    /// <summary>A message read back, or null if it is none.</summary>
    public static (string Player, string Named)? Read(string message) {
        var bar = message.LastIndexOf('|');
        return bar < 0 ? null : (message.Substring(0, bar), message.Substring(bar + 1));
    }
}
