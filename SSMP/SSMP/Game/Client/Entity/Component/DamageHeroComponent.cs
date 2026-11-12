using System.Linq;
using SSMP.Networking.Client;
using SSMP.Networking.Packet.Data;
using UnityEngine;

namespace SSMP.Game.Client.Entity.Component;

/// <inheritdoc />
/// This component manages the damage that an entity deals to the player.
internal class DamageHeroComponent : EntityComponent {
    /// <summary>
    /// Damage components on the host object, in hierarchy order.
    /// </summary>
    private readonly DamageHero[] _hostDamageHeroes;

    /// <summary>
    /// Damage components on the client clone, in hierarchy order.
    /// </summary>
    private readonly DamageHero[] _clientDamageHeroes;

    /// <summary>
    /// Last replicated damage values, in hierarchy order.
    /// </summary>
    private readonly int[] _lastDamageDealt;

    /// <summary>
    /// Last replicated active states, in hierarchy order.
    /// </summary>
    private readonly bool[] _lastActive;

    /// <summary>
    /// Last replicated enabled states of the damage components themselves, in hierarchy order: an FSM's SetDamageHero
    /// switches the component, not its object (the Ant Trapper turns hers off between her attacks; her copy kept it on,
    /// and the friend was hurt by touching her where the host's game showed her harmless).
    /// </summary>
    private readonly bool[] _lastEnabled;
    private float _lastRefresh;

    /// <summary>
    /// (coop) A copy's: whether each box was on in the scene host's last data (DeathOrder.WaitsForBox).
    /// </summary>
    private readonly bool[] _sentOn;

    /// <summary>
    /// (coop) A copy's: the frame the scene host's data last turned one of its boxes on, or -1; a death in that frame waits
    /// for the next, so the box shows (DeathOrder.WaitsForBox).
    /// </summary>
    internal int BoxOnFrame { get; private set; } = -1;

    /// <summary>
    /// (coop) Print only: this game's DamageHero data sent in all, and of it the sends only the 0.05 s refresh made (no
    /// change), with their bytes; read by a probe's getstatic to size the refresh's network cost.
    /// </summary>
    internal static long Sends, Bytes, RefreshOnlySends, RefreshOnlyBytes;

    public DamageHeroComponent(
        NetClient netClient,
        ushort entityId,
        HostClientPair<GameObject> gameObject,
        DamageHero[]? hostDamageHeroes,
        DamageHero[]? clientDamageHeroes
    ) : base(netClient, entityId, gameObject) {
        _hostDamageHeroes = hostDamageHeroes ?? [];
        _clientDamageHeroes = clientDamageHeroes ?? [];
        _lastDamageDealt = _hostDamageHeroes.Select(damageHero => damageHero != null ? damageHero.damageDealt : 0)
                                            .ToArray();
        _lastActive = _hostDamageHeroes.Select(damageHero =>
            damageHero != null && damageHero.gameObject != null && damageHero.gameObject.activeSelf
        ).ToArray();
        _lastEnabled = _hostDamageHeroes.Select(damageHero => damageHero != null && damageHero.enabled).ToArray();
        _sentOn = new bool[_hostDamageHeroes.Length];
    }

    internal bool OwnsActivation(GameObject? target) {
        if (target == null || target == GameObject.Host) {
            return false;
        }
        for (var i = 0; i < _hostDamageHeroes.Length && i < _clientDamageHeroes.Length; i++) {
            if (_hostDamageHeroes[i] != null && _hostDamageHeroes[i].gameObject == target &&
                _clientDamageHeroes[i] != null && _clientDamageHeroes[i].gameObject != null) {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Callback method to check for damage hero updates.
    /// </summary>
    /// <inheritdoc />
    public override void OnUpdate() {
        if (IsControlled) {
            return;
        }

        if (GameObject.Host == null) {
            return;
        }

        var changed = false;
        for (var i = 0; i < _hostDamageHeroes.Length; i++) {
            var damageHero = _hostDamageHeroes[i];
            if (damageHero == null || damageHero.gameObject == null) {
                continue;
            }

            var damageDealt = damageHero.damageDealt;
            var active = damageHero.gameObject.activeSelf;
            var enabled = SyncsEnabled() ? damageHero.enabled : _lastEnabled[i];
            if (damageDealt == _lastDamageDealt[i] && active == _lastActive[i] && enabled == _lastEnabled[i]) {
                continue;
            }

            _lastDamageDealt[i] = damageDealt;
            _lastActive[i] = active;
            _lastEnabled[i] = enabled;
            changed = true;
        }

        var refresh = DamagePairs.RefreshDue(Time.unscaledTime, _lastRefresh);
        if (!changed && !refresh) {
            return;
        }
        if (refresh) _lastRefresh = Time.unscaledTime;

        var data = new EntityNetworkData {
            Type = EntityComponentType.DamageHero
        };
        data.Packet.Write((byte) _hostDamageHeroes.Length);
        foreach (var damageHero in _hostDamageHeroes) {
            data.Packet.Write((byte) (damageHero != null ? damageHero.damageDealt : 0));
            data.Packet.Write(damageHero != null && damageHero.gameObject != null && damageHero.gameObject.activeSelf);
            data.Packet.Write(damageHero != null && (SyncsEnabled() ? damageHero.enabled : true));
        }

        Sends++;
        Bytes += data.Packet.Length;
        if (!changed) {
            RefreshOnlySends++;
            RefreshOnlyBytes += data.Packet.Length;
        }

        SendData(data);
    }

    /// <inheritdoc />
    protected override void InitializeHost() {
    }

    /// <inheritdoc />
    public override void Update(EntityNetworkData data, bool alreadyInSceneUpdate) {
        var length = data.Packet.ReadByte();
        for (var i = 0; i < length; i++) {
            var damageDealt = data.Packet.ReadByte();
            var active = data.Packet.ReadBool();
            var enabled = data.Packet.ReadBool();
            if (i < _sentOn.Length) {
                if (active && enabled && !_sentOn[i]) BoxOnFrame = Time.frameCount;
                _sentOn[i] = active && enabled;
            }

            if (i < _hostDamageHeroes.Length && _hostDamageHeroes[i] != null &&
                _hostDamageHeroes[i].gameObject != null) {
                _hostDamageHeroes[i].damageDealt = damageDealt;
                if (DamagePairs.CopiesActivation(_hostDamageHeroes[i].gameObject == GameObject.Host)) {
                    _hostDamageHeroes[i].gameObject.SetActive(active);
                }
                if (SyncsEnabled()) _hostDamageHeroes[i].enabled = enabled;
            }

            if (i < _clientDamageHeroes.Length && _clientDamageHeroes[i] != null &&
                _clientDamageHeroes[i].gameObject != null) {
                _clientDamageHeroes[i].damageDealt = damageDealt;
                if (DamagePairs.CopiesActivation(_clientDamageHeroes[i].gameObject == GameObject.Client)) {
                    _clientDamageHeroes[i].gameObject.SetActive(active);
                }
                if (SyncsEnabled()) _clientDamageHeroes[i].enabled = enabled;
            }
        }
    }

    /// <summary>
    /// Whether the components' enabled states are synced. Not inlined: the hitbox_damage check's twin skips it, which
    /// gives false, as before this was synced.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static bool SyncsEnabled() => true;

    /// <inheritdoc />
    public override void Destroy() {
    }
}
