using System.Collections.Generic;

namespace SSMP.Game.Server.Save;

/// <summary>
/// The numbers of the save updates the server applied from one player, so an update that comes again (held over a
/// dropped connection, or resent on UDP) is applied once: a shared count is not raised twice. Kept free of Unity types
/// so it can be tested without the game.
/// </summary>
internal class AppliedSaveNumbers {
    /// <summary>
    /// How many of the latest numbers are kept.
    /// </summary>
    public const int Keep = 4096;

    private readonly HashSet<ulong> _seen = [];
    private readonly Queue<ulong> _order = new();

    /// <summary>
    /// The highest number no longer kept; one at or below it counts as applied long ago.
    /// </summary>
    private ulong _floor;

    /// <summary>
    /// Whether the update with this number is new, which also marks it applied.
    /// </summary>
    public bool TryAdd(ulong number) {
        if (number <= _floor || !_seen.Add(number)) {
            return false;
        }

        _order.Enqueue(number);
        if (_order.Count > Keep) {
            var gone = _order.Dequeue();
            _seen.Remove(gone);
            if (gone > _floor) {
                _floor = gone;
            }
        }

        return true;
    }
}
