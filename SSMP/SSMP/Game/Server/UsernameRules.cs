using System.Linq;

namespace SSMP.Game.Server;

/// <summary>
/// Which names a player can connect with. Kept free of Unity types so it can be tested without the game.
/// </summary>
internal static class UsernameRules {
    /// <summary>
    /// The longest name the server accepts.
    /// </summary>
    public const int MaxLength = 20;

    /// <summary>
    /// Whether a name passes the connect screen's rule (not empty, at most <see cref="MaxLength"/> characters) and
    /// the server's (letters and digits only).
    /// </summary>
    public static bool IsValid(string name) =>
        name.Length is > 0 and <= MaxLength && name.All(char.IsLetterOrDigit);

    /// <summary>
    /// The name to connect with: the typed one, or for an empty box the player's Steam name when the server would
    /// accept it; otherwise the empty box, so the connect screen shows its "You must enter a username".
    /// </summary>
    /// <param name="typed">The name box's text.</param>
    /// <param name="steamName">The player's Steam name, or null without Steam.</param>
    public static string Choose(string typed, string? steamName) =>
        typed.Length == 0 && steamName != null && IsValid(steamName) ? steamName : typed;
}
