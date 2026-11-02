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

        if (!changed) {
            return;
        }

        var data = new EntityNetworkData {
            Type = EntityComponentType.DamageHero
        };
        data.Packet.Write((byte) _hostDamageHeroes.Length);
        foreach (var damageHero in _hostDamageHeroes) {
            data.Packet.Write((byte) (damageHero != null ? damageHero.damageDealt : 0));
            data.Packet.Write(damageHero != null && damageHero.gameObject != null && damageHero.gameObject.activeSelf);
            data.Packet.Write(damageHero != null && (SyncsEnabled() ? damageHero.enabled : true));
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
            if (i < _hostDamageHeroes.Length && _hostDamageHeroes[i] != null &&
                _hostDamageHeroes[i].gameObject != null) {
                _hostDamageHeroes[i].damageDealt = damageDealt;
                _hostDamageHeroes[i].gameObject.SetActive(active);
                if (SyncsEnabled()) _hostDamageHeroes[i].enabled = enabled;
            }

            if (i < _clientDamageHeroes.Length && _clientDamageHeroes[i] != null &&
                _clientDamageHeroes[i].gameObject != null) {
                _clientDamageHeroes[i].damageDealt = damageDealt;
                _clientDamageHeroes[i].gameObject.SetActive(active);
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
