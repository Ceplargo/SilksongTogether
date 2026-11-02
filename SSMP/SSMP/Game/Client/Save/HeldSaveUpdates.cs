using System;
using System.Collections.Generic;
using System.Linq;

namespace SSMP.Game.Client.Save;

/// <summary>
/// The save updates this game sent in the last minute, numbered, so the ones a dropped connection lost can go out
/// again when the player joins the same host again. A friend's game sends its changes for up to 5 s after a Wi-Fi
/// drop before it notices, and the server's older copy then came back with the join. The server applies each number
/// once, so an update it did get is not applied twice. Kept free of Unity types so it can be tested without the game.
/// </summary>
internal class HeldSaveUpdates {
    /// <summary>
    /// How long before the latest update an update is still held.
    /// </summary>
    public const double KeepSeconds = 60;

    private readonly List<Held> _held = [];

    /// <summary>
    /// The last number given out. It starts at the clock, so a restarted game numbers past every earlier one.
    /// </summary>
    private ulong _last = (ulong) DateTime.UtcNow.Ticks;

    /// <summary>
    /// The server run the held updates were sent to, once this game has joined one.
    /// </summary>
    private ulong? _runId;

    /// <summary>
    /// Hold an update this game sends now, and give it its number.
    /// </summary>
    /// <param name="index">The save data index.</param>
    /// <param name="value">The encoded value as sent.</param>
    /// <param name="delta">Whether the value is a change to add (every one counts) rather than the new value (only
    /// the latest per index counts).</param>
    /// <param name="now">The time in seconds.</param>
    /// <param name="claim">(coop) The one-of-a-kind action the update belongs to, or "" (decision "The lock").</param>
    /// <returns>The update's number.</returns>
    public ulong Add(ushort index, byte[] value, bool delta, double now, string claim = "") {
        lock (_held) {
            var number = ++_last;
            _held.RemoveAll(h => h.Time < now - KeepSeconds || !delta && !h.Delta && h.Index == index);
            _held.Add(new Held(now, number, index, value, delta, claim));
            return number;
        }
    }

    /// <summary>
    /// What to offer the host at a join: the run the updates were sent to and the held updates, oldest first; null
    /// when nothing is held or this game has joined no host yet.
    /// </summary>
    public (ulong RunId, List<(ulong Number, ushort Index, byte[] Value, string Claim)> Updates)? ForRejoin() {
        lock (_held) {
            if (_runId is not { } runId || _held.Count == 0) {
                return null;
            }

            return (runId, _held.Select(h => (h.Number, h.Index, h.Value, h.Claim)).ToList());
        }
    }

    /// <summary>
    /// A join was accepted: the host took the held updates (or they belong to another run), so they are dropped, and
    /// the updates from now on belong to this run.
    /// </summary>
    /// <param name="runId">The server's run.</param>
    public void Connected(ulong runId) {
        lock (_held) {
            _held.Clear();
            _runId = runId;
        }
    }

    private readonly record struct Held(double Time, ulong Number, ushort Index, byte[] Value, bool Delta, string Claim);
}
