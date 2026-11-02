using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace SSMP.Game.Server;

/// <summary>
/// The line a player's /bug puts into every player's log. Kept free of Unity types so it can be tested without the
/// game.
/// </summary>
internal static class BugMark {
    /// <summary>
    /// How every bug mark starts, in the chat and in the logs.
    /// </summary>
    public const string Prefix = "[BUG] ";

    /// <summary>
    /// The longest note a mark keeps.
    /// </summary>
    public const int MaxNote = 80;

    /// <summary>
    /// A note a player typed, safe for one log line: control characters and line or paragraph separators become
    /// spaces, runs of spaces one, trimmed, and cut to <see cref="MaxNote"/> characters. The note comes from a
    /// client, so nothing in it may start a line of its own.
    /// </summary>
    /// <param name="raw">The note as typed, or null.</param>
    /// <param name="maxLength">The longest result.</param>
    public static string Clean(string? raw, int maxLength = MaxNote) {
        var text = new StringBuilder();
        foreach (var c in raw ?? "") {
            var space = char.IsControl(c) || char.IsWhiteSpace(c) ||
                        char.GetUnicodeCategory(c) is UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator;
            if (!space) {
                text.Append(c);
            } else if (text.Length > 0 && text[text.Length - 1] != ' ') {
                text.Append(' ');
            }
        }

        var cleaned = text.ToString().TrimEnd();
        return cleaned.Length <= maxLength ? cleaned : cleaned.Substring(0, maxLength).TrimEnd();
    }

    /// <summary>
    /// The mark: when, who typed it, their note, and the room each player is in.
    /// </summary>
    /// <param name="time">The host's clock when the mark was made.</param>
    /// <param name="who">The name of the player who typed it.</param>
    /// <param name="note">The note as typed, or null.</param>
    /// <param name="players">Every player's name and room (null before their first room).</param>
    public static string Line(DateTime time, string who, string? note, IEnumerable<(string Name, string? Room)> players) {
        var cleaned = Clean(note);
        var rooms = string.Join(", ", players.Select(p => $"{p.Name} in {p.Room ?? "no room yet"}"));
        return $"{Prefix}{time:yyyy-MM-dd HH:mm:ss} {who}" + (cleaned.Length > 0 ? $": \"{cleaned}\"" : "") +
               $" | {rooms}";
    }
}
