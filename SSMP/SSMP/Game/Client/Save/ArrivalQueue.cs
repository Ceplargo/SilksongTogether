using System;
using System.Collections.Generic;

namespace SSMP.Game.Client.Save;

/// <summary>
/// PlayerData values that arrived from other players and still need the game's own refresh (the HUD, derived
/// values). They wait while the hero is not in control (paused, in a menu, dead, between scenes). Kept free of Unity
/// types so it can be tested without the game.
/// </summary>
internal sealed class ArrivalQueue {
    /// <summary>
    /// The names waiting for a refresh, in arrival order, each at most once.
    /// </summary>
    private readonly List<string> _names = [];

    /// <summary>
    /// Queue a value for a refresh; a value that is already waiting is not queued twice.
    /// </summary>
    public void Add(string name) {
        if (!_names.Contains(name)) {
            _names.Add(name);
        }
    }

    /// <summary>
    /// The waiting names, in arrival order, if the hero is in control; they then leave the queue.
    /// </summary>
    public IReadOnlyList<string> Drain(bool heroInControl) {
        if (!heroInControl || _names.Count == 0) {
            return Array.Empty<string>();
        }

        var names = _names.ToArray();
        _names.Clear();
        return names;
    }
}
