using System;
using System.Collections.Generic;
using System.Linq;

namespace BossRule;

/// <summary>
/// A player the server knows of, whether they are still connected, and whether they stand where the fight starts.
/// </summary>
internal record PlayerPresence(ushort Id, string Name, bool Connected, bool InArea);

/// <summary>
/// Whether a boss fight may start, who is missing (in alphabetical order) and how many of the connected players are in.
/// </summary>
internal record WaitResult(bool Allowed, IReadOnlyList<string> Missing, int Present, int Total);

/// <summary>
/// The waiting rule: a listed boss fight starts only when every connected player is in its area. Unity-free.
/// </summary>
internal static class WaitRule {
    public static WaitResult Evaluate(IReadOnlyCollection<PlayerPresence> players) {
        var connected = players.Where(p => p.Connected).ToList();
        var missing = connected.Where(p => !p.InArea).Select(p => p.Name)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();
        return new WaitResult(missing.Count == 0, missing, connected.Count - missing.Count, connected.Count);
    }

    /// <summary>
    /// (coop) An ordinary wave arena's wait (ArenaHold): only the heroes in its room count, so a player elsewhere in the
    /// world does not hold the start (the room's runner is then the one inside anyway). The project's ruling,
    /// 2026-11-20: Room_CrowCourt's arena started alone in a game that does not run the room and shut its hero in.
    /// </summary>
    public static WaitResult EvaluateRoom(IEnumerable<(PlayerPresence Player, bool InRoom)> players) =>
        Evaluate(players.Where(p => p.InRoom).Select(p => p.Player).ToList());

    /// <summary>
    /// (coop) How long an ordinary arena's start waits for the heroes in its room before it starts for all anyway (the
    /// owner, 2026-11-20: "it waits a few seconds for the other player to enter since it's a timing event, along with
    /// arenas"). One number for every such wait: World A's Lava Challenge door uses it too.
    /// </summary>
    public const float RoomWaitS = 5f;

    /// <summary>An ordinary arena's held start goes once everyone in the room is in, or RoomWaitS after it was held.</summary>
    public static bool RoomMayStart(WaitResult result, float heldS) => result.Allowed || heldS >= RoomWaitS;

    /// <summary>The chat line while an ordinary arena waits: <c>Waiting for Bob, 4 s</c> (whole seconds left, at least 1).</summary>
    public static string RoomMessage(WaitResult result, float heldS) =>
        $"Waiting for {string.Join(", ", result.Missing)}, {Math.Max(1, (int)Math.Ceiling(RoomWaitS - heldS))} s";

    /// <summary>
    /// The chat line while the fight waits: <c>Waiting for Bob, Carol (1 of 3 in the arena)</c>.
    /// </summary>
    public static string Message(WaitResult result) =>
        $"Waiting for {string.Join(", ", result.Missing)} ({result.Present} of {result.Total} in the arena)";
}
