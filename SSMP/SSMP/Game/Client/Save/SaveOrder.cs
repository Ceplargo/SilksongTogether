using System.Collections.Generic;

namespace SSMP.Game.Client.Save;

/// <summary>
/// (coop, 0.4 World A) One order for the shared save: the server's arrival order. Two games writing one shared value at
/// once each took the other's and they ended crossed (probe_setpd_once: "set host 2, friend 5 at once; 3 s later host 5,
/// friend 2", 3 of 3), as both games' TimePasses at a followed crest's reload did. The server now numbers each shared
/// write it keeps (its place in the order) and answers the writing game with that number; this game applies another's
/// write only when it is later than what it has (its own last answered write or the last it applied), and holds one that
/// came while its own were on the way until they are answered. Unity-free, so it is tested without the game (the launching
/// chat's ruling A, 2026-11-18).
/// </summary>
internal sealed class SaveOrder {
    private readonly Dictionary<ushort, int> _pending = new();
    private readonly Dictionary<ushort, ulong> _last = new();
    private readonly Dictionary<ushort, (ulong Number, byte[] Value)> _held = new();

    /// <summary>The connection ended or began: no write is on its way and no order is known.</summary>
    public void Clear() {
        _pending.Clear();
        _last.Clear();
        _held.Clear();
    }

    /// <summary>This game sent a write of index (whole, not an added change): it is on its way until answered.</summary>
    public void Sent(ushort index) => _pending[index] = Pending(index) + 1;

    /// <summary>
    /// The server's answer to one of this game's writes of index: number, its place in the order (0: not kept or not
    /// ordered). Returns another game's write to apply now (one held while this game's own were on their way, ordered
    /// after them), else null.
    /// </summary>
    public byte[]? Own(ushort index, ulong number) {
        if (Pending(index) > 0) _pending[index] = Pending(index) - 1;
        if (number > Last(index)) _last[index] = number;
        if (Pending(index) > 0 || !_held.TryGetValue(index, out var held)) return null;
        _held.Remove(index);
        if (held.Number <= Last(index)) return null;
        _last[index] = held.Number;
        return held.Value;
    }

    /// <summary>
    /// Another game's write of index, at number in the server's order (0: none, applied as before): whether it is
    /// applied now. Never one older than what this game has; held while this game's own write is on its way.
    /// </summary>
    public bool Other(ushort index, ulong number, byte[] value) {
        if (number == 0) return true;
        if (number <= Last(index)) return false;
        if (Pending(index) > 0) {
            if (!_held.TryGetValue(index, out var held) || held.Number < number) _held[index] = (number, value);
            return false;
        }

        _last[index] = number;
        return true;
    }

    private int Pending(ushort index) => _pending.TryGetValue(index, out var n) ? n : 0;

    private ulong Last(ushort index) => _last.TryGetValue(index, out var n) ? n : 0;
}
