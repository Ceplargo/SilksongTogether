using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

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
    /// The waiting names whose arrivals all came with the join's snapshot (a load, not a pickup).
    /// </summary>
    private readonly HashSet<string> _loads = [];

    /// <summary>
    /// Queue a value for a refresh; a value that is already waiting is not queued twice.
    /// </summary>
    public void Add(string name) {
        if (!_names.Contains(name)) {
            _names.Add(name);
        }
    }

    /// <summary>
    /// Queue a value for a refresh only if it changes this game's own value (0.3.22): the join's snapshot brings
    /// every value, and an unchanged silkMax's refresh (SPOOL MAX UP) mended the friend's broken silk spool.
    /// </summary>
    /// (0.3.23 K8) An arrival in the join's snapshot is refreshed as a load: the joining game's own value is its
    /// stale file's (the friend's silkMax 9, the server's 10, after the friend's own piece), and the pickup's refresh
    /// (SPOOL MAX UP) mended a spool the game alone keeps broken across a load (GameManager.FixUpSaveState, a cocoon
    /// left). A live arrival after it makes the waiting refresh a pickup's again.
    public void Add(string name, object? own, object? arrived, bool joining = false) {
        if (SameAsOwn(own, arrived)) {
            return;
        }

        var waiting = _names.Contains(name);
        Add(name);
        if (JoinIsLoad(joining) && (!waiting || _loads.Contains(name))) {
            _loads.Add(name);
        } else {
            _loads.Remove(name);
        }
    }

    /// <summary>
    /// Whether an arrival with the join's snapshot is refreshed as a load. Its own method so spool_kept's twin can
    /// skip it (every arrival refreshed as a pickup, as in 0.3.22).
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool JoinIsLoad(bool joining) => joining;

    /// <summary>
    /// Whether an arrived value equals this game's own. Its own method so a check's twin can skip it (every arrival
    /// refreshed again, as before 0.3.22).
    /// </summary>
    public static bool SameAsOwn(object? own, object? arrived) => Equals(own, arrived);

    /// <summary>
    /// The waiting names, in arrival order, if the hero is in control; they then leave the queue.
    /// </summary>
    public IReadOnlyList<(string Name, bool Load)> DrainEach(bool heroInControl) {
        var each = new List<(string, bool)>();
        var loads = new HashSet<string>(_loads);
        foreach (var name in Drain(heroInControl)) each.Add((name, loads.Contains(name)));
        return each;
    }

    public IReadOnlyList<string> Drain(bool heroInControl) {
        if (!heroInControl || _names.Count == 0) {
            return Array.Empty<string>();
        }

        var names = _names.ToArray();
        _names.Clear();
        _loads.Clear();
        return names;
    }
}
