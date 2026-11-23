namespace BossRule;

/// <summary>
/// (0.4 enemies A, 3c A; the owner's Inbox line of 2026-11-11) A player's hero hidden in its own game, told to the room
/// (<see cref="HeroHides"/>): the message, when it goes, and which one a game keeps. Unity-free, so BossRule.Tests
/// compiles it without the game.
/// </summary>
internal static class HeroHide {
    public const string Prefix = "Hero Shown: ";

    /// <summary>The room state: the player's name, 1 if its hero shows and 0 if hidden, and the sender's count.</summary>
    public static string Message(string name, bool shown, int seq) => $"{name}|{(shown ? 1 : 0)}|{seq}";

    /// <summary>A received state's name, whether that hero shows and its count; null if it is not one.</summary>
    public static (string Name, bool Shown, int Seq)? Parse(string text) {
        var last = text.LastIndexOf('|');
        var bar = last > 0 ? text.LastIndexOf('|', last - 1) : -1;
        if (bar <= 0 || last != bar + 2 || text[bar + 1] is not ('0' or '1') ||
            !int.TryParse(text.Substring(last + 1), out var seq)) return null;
        return (text.Substring(0, bar), text[bar + 1] == '1', seq);
    }

    /// <summary>
    /// Whether this game tells the room now: on a change only, never in a scene change (SSMP shows a player's copy as it
    /// comes into a room), and a room's first look only when hidden (a copy comes in shown).
    /// </summary>
    public static bool Sends(bool shown, bool? last, bool transitioning) =>
        !transitioning && last != shown && (last != null || !shown);

    /// <summary>Whether a received count is newer than the last kept from that player (an older one under lag is
    /// dropped: a hide then show 0.1 s apart must end shown).</summary>
    public static bool Newer(int seq, int? last) => last == null || seq > last;
}
