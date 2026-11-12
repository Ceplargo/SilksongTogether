using System;
using System.Collections.Generic;

namespace SSMP.Game.Client.Entity.Component;

/// <summary>
/// (coop, 0.3.21) Loaded things by name, scanned again when a name is not known yet or its thing is gone: a copy finds the
/// clip the scene host's loop plays (<see cref="AudioLoopComponent"/>). Unity-free, so it is tested without the game.
/// </summary>
internal sealed class ByName<T> where T : class {
    private readonly Func<IEnumerable<T>> _scan;
    private readonly Func<T, string> _name;
    private readonly Func<T, bool> _alive;
    private Dictionary<string, T> _found = new();
    private readonly HashSet<string> _missed = new();

    public ByName(Func<IEnumerable<T>> scan, Func<T, string> name, Func<T, bool> alive) {
        _scan = scan;
        _name = name;
        _alive = alive;
    }

    /// <summary>The live thing of that name, or null.</summary>
    public T? Find(string name) {
        if (_found.TryGetValue(name, out var thing) && _alive(thing)) return thing;
        if (_missed.Contains(name)) return null;
        _found = new Dictionary<string, T>();
        foreach (var each in _scan()) {
            if (_alive(each)) _found.TryAdd(_name(each), each);
        }

        if (_found.TryGetValue(name, out thing)) return thing;
        // shortcut: a name missing once is never scanned for again, as a scan reads every loaded thing; clear it on a
        // scene change if a copy's loop is ever found missing a clip its room loads later
        _missed.Add(name);
        return null;
    }
}
