using System.Runtime.CompilerServices;
using SSMP.Networking.Client;
using SSMP.Networking.Packet.Data;
using UnityEngine;

namespace SSMP.Game.Client.Entity.Component;

/// <inheritdoc />
/// (coop) This component keeps an enemy's layer for a take-over. The scene host's game sends the enemy's layer, at first
/// and as its FSM changes it; the other games keep it (one that comes into the room later gets the last from the
/// server); and a game that takes the room over gives it to the original. The Dock Guard Slasher's FSM gives him his
/// fighting layer only at his intro's end, which the hidden original never ran, so after a take-over the floor did not
/// hold him: he fell out of the room and died (all ten fights of round three's probe). (0.3.21) The copy here takes
/// the scene host's layer too as it arrives: a wave enemy the room's game holds back in the background (Bone_01's
/// Wave 2 Bone Goomba, Ignore Raycast in its BG Hiding) kept its Enemies layer here, so the other player saw it as a
/// live enemy and killed it (probe_wave_copy).
internal class LayerComponent : EntityComponent {
    /// <summary>
    /// Scene host: the layer last sent, -1 before the first.
    /// </summary>
    private int _sentLayer = -1;

    /// <summary>
    /// Other games: the scene host's layer for the enemy, kept for a take-over.
    /// </summary>
    private int? _hostLayer;

    public LayerComponent(
        NetClient netClient,
        ushort entityId,
        HostClientPair<GameObject> gameObject
    ) : base(netClient, entityId, gameObject) {
    }

    /// <inheritdoc />
    public override void OnUpdate() {
        if (IsControlled || GameObject.Host == null || GameObject.Host.layer == _sentLayer) {
            return;
        }

        _sentLayer = GameObject.Host.layer;
        var data = new EntityNetworkData {
            Type = EntityComponentType.Layer
        };
        data.Packet.Write((byte) _sentLayer);

        SendData(data);
    }

    /// <inheritdoc />
    public override void InitializeHost(uint sceneHostEpoch) {
        if (_hostLayer is { } layer && GameObject.Host != null) {
            TakesHostLayer(GameObject.Host, layer);
        }

        _hostLayer = null;
        _sentLayer = -1;
    }

    /// <summary>
    /// (coop) A taken-over enemy gets the scene host's layer. Not inlined: slasher_handover's twin skips it.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void TakesHostLayer(GameObject original, int layer) => original.layer = layer;

    /// <summary>
    /// (coop, 0.3.21) The copy here gets the scene host's layer for the enemy. Not inlined: wave_copy's twin skips it.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CopyTakesHostLayer(GameObject copy, int layer) => copy.layer = layer;

    /// <inheritdoc />
    public override void Update(EntityNetworkData data, bool alreadyInSceneUpdate) {
        if (!IsControlled) {
            return;
        }

        _hostLayer = data.Packet.ReadByte();
        if (GameObject.Client != null) {
            CopyTakesHostLayer(GameObject.Client, _hostLayer.Value);
        }
    }

    /// <inheritdoc />
    public override void Destroy() {
    }
}
