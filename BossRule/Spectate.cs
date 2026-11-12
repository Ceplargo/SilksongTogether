using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GlobalEnums;
using HarmonyLib;
using SSMP.Api.Client;
using UnityEngine;

namespace BossRule;

/// <summary>
/// Watching a listed boss fight after going down, in each player's game: instead of dying, the player watches a
/// friend until the fight is won (back next to a friend, full health, rosaries kept, no cocoon) or everyone is down
/// (the game's own death after all). The server decides which, from every fighter's down (the last one's too).
/// </summary>
internal sealed class Spectate {
    public static Spectate? Instance { get; private set; }

    /// <summary>
    /// Whether the local player watches a boss fight.
    /// </summary>
    public static bool Watching => Instance?._watching ?? false;

    /// <summary>
    /// (coop) Whether a boss fight is on in this room (a friend's talk then goes to the chat, not over the fight).
    /// </summary>
    public static bool FightOnIn(string scene) => Instance?._scene == scene;

    private static bool _bypass;

    /// <summary>
    /// Whether this game is on its way to the main menu: the player's Quit to Menu, or SSMP after a lost connection.
    /// Set by a prefix on both games' ReturnToMainMenu, cleared on the next connect.
    /// </summary>
    public static bool Leaving;

    private readonly IClientManager _client;
    private readonly IReadOnlyList<BossEntry> _bosses;
    private readonly Action<string> _sendDowned;
    private readonly Action<string> _sendDefeated;
    private string? _scene;
    private List<string> _flags = new();
    private HashSet<string> _beaten = new();
    private ushort _self;
    private HashSet<ushort> _fighters = new();
    private readonly HashSet<ushort> _watchers = new();
    private bool _watching;
    private bool _leftWhileWatching;
    private Vector3 _deathPosition;

    public Spectate(IClientManager client, IReadOnlyList<BossEntry> bosses, Action<string> sendDowned, Action<string> sendDefeated) {
        _client = client;
        _bosses = bosses;
        _sendDowned = sendDowned;
        _sendDefeated = sendDefeated;
        Instance = this;
    }

    public void OnFightStarted(FightStarted packet) {
        _scene = packet.Scene;
        // Any of the room's boss flags turning true is the win, should the scene host have held another entry's start
        _flags = _bosses.Where(e => e.Scene == packet.Scene).Select(e => e.Flag).Append(packet.Flag).Distinct().ToList();
        _beaten = new HashSet<string>(_flags.Where(Beaten));
        _self = packet.Self;
        _fighters = new HashSet<ushort>(packet.Fighters);
        _watchers.Clear();
        Debug.Log($"[BossRule] The fight in {packet.Scene} is on: {_fighters.Count} fighting");
    }

    public void OnOutcome(Outcome packet) {
        if (packet.Scene != _scene) return;

        var kind = (OutcomeKind) packet.Kind;
        if (kind == OutcomeKind.Watch) {
            foreach (var id in packet.Players) {
                _watchers.Add(id);
                if (id != _self) SetShown(id, false);
            }

            return;
        }

        // Won or wiped: the fight is over, for a watcher the outcome does not name too (its down came too late)
        foreach (var id in _watchers.Where(id => id != _self)) SetShown(id, true);
        var action = OutcomeRule.ForLocal(_watching, kind);
        End();
        if (action == ReturnAction.ComeBack) ComeBack();
        else if (action == ReturnAction.DieAfterAll) DieAfterAll();
    }

    /// <summary>
    /// The connection ended: a watcher, now alone, dies the normal way, unless the game is leaving for the main menu.
    /// SSMP ends the connection as a quit starts, before the game's own menu call, so this is decided on the next
    /// frame; a death started now would turn pause off, and the game would cancel the quit.
    /// </summary>
    public void OnDisconnect() {
        var watching = _watching;
        End();
        if (watching) _leftWhileWatching = true;
    }

    public static void LeavingPrefix() => Leaving = true;

    /// <summary>
    /// Once per frame: report the boss beaten when its flag turns true in this game (the server keeps the first
    /// report), and keep the camera on a friend who still fights.
    /// </summary>
    public void Tick() {
        if (_leftWhileWatching) {
            _leftWhileWatching = false;
            if (Leaving || HeroController.SilentInstance == null) {
                StopWatching();
            } else {
                DieAfterAll();
            }
        }

        if (_scene == null) return;

        foreach (var flag in _flags.Where(f => !_beaten.Contains(f) && Beaten(f)).ToList()) {
            _beaten.Add(flag);
            _sendDefeated(_scene);
        }

        if (_watching) SpectateCamera.Follow(FriendInFight());
    }

    private static bool Beaten(string flag) => PlayerData.instance != null && PlayerData.instance.GetBool(flag);

    private void End() {
        _scene = null;
        _fighters.Clear();
        _watchers.Clear();
    }

    /// <summary>
    /// Prefix on <c>HeroController.Die(bool nonLethal, bool frostDeath)</c>: a fighter who goes down while a friend
    /// still fights watches instead.
    /// </summary>
    public static bool DiePrefix(HeroController __instance, bool nonLethal, ref IEnumerator __result) {
        if (_bypass) {
            _bypass = false;
            return true;
        }

        // While watching, the game can call its death again (damage ticks such as acid or frost): nothing happens
        if (Instance?._watching == true) {
            __result = Nothing();
            return false;
        }

        var s = Instance;
        if (s?._scene == null || nonLethal || !s._fighters.Contains(s._self) ||
            GameManager.instance == null || GameManager.instance.sceneName != s._scene) {
            return true;
        }

        // The server needs every fighter's down, the last one's too, to tell a wipe from a watch
        s._sendDowned(s._scene);
        var friendUp = s._fighters.Any(id => id != s._self && !s._watchers.Contains(id) && s._client.TryGetPlayer(id, out _));
        if (!friendUp) return true;

        s.StartWatching(__instance);
        __result = Nothing();
        return false;
    }

    private static IEnumerator Nothing() {
        yield break;
    }

    private void StartWatching(HeroController hero) {
        _watching = true;
        _watchers.Add(_self);
        _deathPosition = hero.transform.position;
        hero.SetDamageMode(DamageMode.NO_DAMAGE);
        hero.RelinquishControl();
        hero.AffectedByGravity(false);
        hero.Body.linearVelocity = Vector2.zero;
        hero.Body.bodyType = RigidbodyType2D.Kinematic;
        HeroBox.Inactive = true;
        ShowHero(hero, false);
        _client.SetLocalPlayerTargetable(false);
        SpectateCamera.Follow(FriendInFight());
        Debug.Log("[BossRule] Down in a boss fight: watching");
    }

    private void ComeBack() {
        var hero = HeroController.instance;
        var friend = FriendInFight() ?? AnyFriend();
        hero.transform.position = friend != null ? friend.position + Vector3.right : _deathPosition;
        Recover(hero);
        hero.RefillHealthToMax();
        Debug.Log("[BossRule] The boss was beaten: back in");
    }

    private void DieAfterAll() {
        var hero = HeroController.SilentInstance;
        if (hero == null) {
            StopWatching();
            return;
        }

        hero.transform.position = _deathPosition;
        Recover(hero);
        _bypass = true;
        hero.StartCoroutine((IEnumerator) AccessTools.Method(typeof(HeroController), "Die").Invoke(hero, [false, false]));
        Debug.Log("[BossRule] Everyone is down: a normal death");
    }

    /// <summary>
    /// Leaving the game while watching: no death on the way out, just the hero back to normal (if still there).
    /// </summary>
    private void StopWatching() {
        var hero = HeroController.SilentInstance;
        if (hero != null) {
            Recover(hero);
        } else {
            _watching = false;
            SpectateCamera.Restore();
            HeroBox.Inactive = false;
            _client.SetLocalPlayerTargetable(true);
        }

        Debug.Log("[BossRule] Left the game while watching: no death");
    }

    private void Recover(HeroController hero) {
        _watching = false;
        SpectateCamera.Restore();
        hero.Body.bodyType = RigidbodyType2D.Dynamic;
        hero.AffectedByGravity(true);
        HeroBox.Inactive = false;
        ShowHero(hero, true);
        hero.SetDamageMode(DamageMode.FULL_DAMAGE);
        hero.RegainControl();
        _client.SetLocalPlayerTargetable(true);
    }

    private static void ShowHero(HeroController hero, bool shown) {
        foreach (var renderer in hero.GetComponents<MeshRenderer>()) renderer.enabled = shown;
    }

    /// <summary>
    /// Hide or show another player's Hornet, name included.
    /// </summary>
    private void SetShown(ushort id, bool shown) {
        if (!_client.TryGetPlayer(id, out var player) || player == null) return;

        var root = player.PlayerContainer != null ? player.PlayerContainer : player.PlayerObject;
        if (root == null) return;

        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) renderer.enabled = shown;
    }

    /// <summary>
    /// A friend in this room who is still fighting: the one to watch.
    /// </summary>
    private Transform? FriendInFight() =>
        _client.Players.Where(p => p.Id != _self && _fighters.Contains(p.Id) && !_watchers.Contains(p.Id) && p.PlayerObject != null)
            .Select(p => p.PlayerObject!.transform).FirstOrDefault();

    private Transform? AnyFriend() =>
        _client.Players.Where(p => p.PlayerObject != null).Select(p => p.PlayerObject!.transform).FirstOrDefault();
}
