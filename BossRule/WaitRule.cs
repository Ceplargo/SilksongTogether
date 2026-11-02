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
    /// The chat line while the fight waits: <c>Waiting for Bob, Carol (1 of 3 in the arena)</c>.
    /// </summary>
    public static string Message(WaitResult result) =>
        $"Waiting for {string.Join(", ", result.Missing)} ({result.Present} of {result.Total} in the arena)";
}
