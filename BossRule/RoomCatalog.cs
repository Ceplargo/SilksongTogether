using System;

namespace BossRule;

/// <summary>
/// (coop) Whether ArenaWins leaves a room alone (the boss catalog lists it, or no catalog is set yet), looked up once per
/// room: its Tick asked the catalog every frame in a catalog room. Unity-free, so it is tested without the game.
/// </summary>
internal sealed class RoomCatalog {
    private string? _room;
    private bool _skipped;

    public bool Skipped(string room, Func<string, bool>? catalog) {
        if (room != _room) {
            _room = room;
            _skipped = catalog?.Invoke(room) != false;
        }

        return _skipped;
    }
}
