using System.Linq;
using SSMP.Networking.Client;
using SSMP.Networking.Packet.Data;
using SSMP.Util;
using UnityEngine;

namespace SSMP.Game.Client.Entity.Component;

/// <summary>The Coral Judge's child objects follow their own native activation, including the first snapshot.</summary>
internal class ChildrenActivationComponent : EntityComponent {
    private readonly GameObject[] _hostChildren;
    private readonly GameObject[] _clientChildren;
    private readonly int[] _pairs;
    private byte[]? _lastActive;

    public ChildrenActivationComponent(
        NetClient netClient,
        ushort entityId,
        HostClientPair<GameObject> gameObject
    ) : base(netClient, entityId, gameObject) {
        _hostChildren = gameObject.Host.GetChildren().ToArray();
        _clientChildren = gameObject.Client.GetChildren().ToArray();
        _pairs = DamagePairs.Align(_hostChildren.Select(child => child.name).ToArray(),
                                  _clientChildren.Select(child => child.name).ToArray());
    }

    public override void OnUpdate() {
        if (IsControlled || GameObject.Host == null || _hostChildren.Length == 0) {
            return;
        }

        var active = PartBits.Pack(_hostChildren.Select(child => child != null && child.activeSelf).ToArray());
        if (!ChildActivation.NeedsUpdate(_lastActive, active)) {
            return;
        }

        _lastActive = active;
        var data = new EntityNetworkData { Type = EntityComponentType.ChildrenActivation };
        data.Packet.Write((byte) active.Length);
        foreach (var part in active) {
            data.Packet.Write(part);
        }
        SendData(data);
    }

    protected override void InitializeHost() {
        _lastActive = null;
    }

    public override void Update(EntityNetworkData data, bool alreadyInSceneUpdate) {
        if (!IsControlled) {
            return;
        }

        var active = new byte[data.Packet.ReadByte()];
        for (var i = 0; i < active.Length; i++) {
            active[i] = data.Packet.ReadByte();
        }
        for (var i = 0; i < _hostChildren.Length && PartBits.Carried(i) && i / 8 < active.Length; i++) {
            var on = PartBits.IsOn(active, i);
            if (_hostChildren[i] != null) {
                _hostChildren[i].SetActive(on);
            }
            var pair = _pairs[i];
            if (pair >= 0 && _clientChildren[pair] != null) {
                _clientChildren[pair].SetActive(on);
            }
        }
    }

    public override void Destroy() {
    }
}
