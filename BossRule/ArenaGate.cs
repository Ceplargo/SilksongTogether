namespace BossRule;

/// <summary>
/// (coop) Whether a game shuts an ordinary arena's gates now (ArenaGates; the owner, 2026-11-20: "letting the 2nd player
/// enter at any time; gate closes for them when they join an ongoing arena"). Unity-free, so it is tested without the game.
/// </summary>
internal static class ArenaGate {
    /// <summary>
    /// Shut now, as the game alone does: outside a session, in a boss catalog room (StartGate's), once won, for an arena
    /// with no area of its own (its start is a detector's or a lever's: there is no inside to wait for), or with this
    /// game's hero inside the arena. Otherwise the gates wait, open, until that hero walks in.
    /// </summary>
    public static bool ShutsNow(bool inSession, bool catalogRoom, bool completed, bool hasArea, bool heroInside) =>
        !inSession || catalogRoom || completed || !hasArea || heroInside;
}
