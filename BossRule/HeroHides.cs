using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace BossRule;

/// <summary>
/// (0.4 enemies A, 3c A; the owner's Inbox line of 2026-11-11: the Widow's kill didn't match on the two screens) A game's
/// own hero hidden is hidden in the other games too. The Widow's Hornet Dash turns the binder's own hero's mesh renderer
/// off (SetMeshRenderer Hero) while her sprite and a Hornet Dummy draw the bind; SSMP keeps a hero-aimed action in its own
/// game (it would hide the other player's hero), so the binder's copy in the other game stood idle in mid-air for 24 s
/// (probe_widow_end, 2026-11-20). Each game tells the room when its own hero's renderer turns off or on (on a change only,
/// never in a scene change, <see cref="HeroHide"/>); the others set that player's copy's renderer the same, and show it
/// again when that player changes room or leaves, or the follow is off. A player who comes into the room gets a hidden
/// hero's state.
/// </summary>
internal static class HeroHides {
    /// <summary>Sends a room state to the room's other players; set by the client add-on.</summary>
    public static Action<string, string>? Send;

    /// <summary>This game's player's name; set by the client add-on.</summary>
    public static Func<string?>? Username;

    /// <summary>The copy of the player of that name in this room, or null; set by the client add-on.</summary>
    public static Func<string, GameObject?>? PlayerObject;

    private static bool? _last;
    private static int _seq;
    private static string? _room;
    private static HeroController? _hero;
    private static MeshRenderer? _renderer;
    private static readonly Dictionary<string, int> Kept = new();
    private static readonly HashSet<string> Hidden = new();

    /// <summary>Every frame: this game's own hero's renderer, told to the room as HeroHide says.</summary>
    public static void Tick() {
        if (!Follows() && Hidden.Count > 0) ShowAll();
        var scene = GameManager.SilentInstance?.sceneName;
        var hero = HeroController.SilentInstance;
        if (scene == null || hero == null) return;
        if (scene != _room) {
            _room = scene;
            _last = null;
        }

        if (hero != _hero) {
            _hero = hero;
            _renderer = hero.GetComponent<MeshRenderer>();
        }

        if (_renderer == null) return;
        var shown = _renderer.enabled;
        var transitioning = hero.cState.transitioning || GameManager.SilentInstance!.IsInSceneTransition;
        if (!HeroHide.Sends(shown, _last, transitioning)) return;
        _last = shown;
        Tell(scene, shown);
    }

    /// <summary>A player came into this room: it gets this game's hero's state if hidden.</summary>
    public static void Joined() {
        if (_last == false && _room != null) Tell(_room, false);
    }

    /// <summary>That player left this room or the game: its copy, if hidden, shows again.</summary>
    public static void Left(string name) {
        Kept.Remove(name);  // a player who comes back counts from 1 again
        if (!Hidden.Remove(name)) return;
        var copy = PlayerObject?.Invoke(name);
        if (copy != null && copy.TryGetComponent<MeshRenderer>(out var renderer)) renderer.enabled = true;
    }

    /// <summary>Another game's hero shown or hidden: its copy here the same, unless an older word.</summary>
    public static void Echo(string text) {
        if (!Follows() || HeroHide.Parse(text) is not var (name, shown, seq)) return;
        if (!HeroHide.Newer(seq, Kept.TryGetValue(name, out var had) ? had : null)) return;
        Kept[name] = seq;
        var copy = PlayerObject?.Invoke(name);
        if (copy == null || !copy.TryGetComponent<MeshRenderer>(out var renderer)) return;
        renderer.enabled = shown;
        if (shown) Hidden.Remove(name);
        else Hidden.Add(name);
    }

    /// <summary>Not inlined: widow_bind's twin skips it, which gives false (a copy drawn while its hero hides, as before).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool Follows() => true;

    private static void Tell(string scene, bool shown) {
        var name = Username?.Invoke();
        if (string.IsNullOrEmpty(name)) return;
        _seq++;
        if (!shown) Debug.Log($"[BossRule] This game's hero is hidden: the room told ({scene})");
        Send?.Invoke(scene, HeroHide.Prefix + HeroHide.Message(name!, shown, _seq));
    }

    private static void ShowAll() {
        foreach (var name in new List<string>(Hidden)) Left(name);
    }
}
