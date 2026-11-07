using System.Linq;
using SSMP.Networking.Client;
using SSMP.Networking.Packet.Data;
using SSMP.Util;
using UnityEngine;

namespace SSMP.Game.Client.Entity.Component;

/// <summary>
/// (coop) An enemy's direct children keep the places within it the scene host's have (ChildPlaces): sent when any moved,
/// set on the copy's children of the same name and on the hidden original's. Requested by the entity registry (the
/// Rhino, for its Shell).
/// </summary>
internal class ChildPlacesComponent : EntityComponent {
    private readonly GameObject[] _hostChildren;
    private readonly GameObject[] _clientChildren;
    private readonly int[] _pairs;
    private float[]? _lastSent;

    public ChildPlacesComponent(NetClient netClient, ushort entityId, HostClientPair<GameObject> gameObject)
        : base(netClient, entityId, gameObject) {
        _hostChildren = gameObject.Host.GetChildren().ToArray();
        _clientChildren = gameObject.Client.GetChildren().ToArray();
        _pairs = DamagePairs.Align(_hostChildren.Select(child => child.name).ToArray(),
                                  _clientChildren.Select(child => child.name).ToArray());
    }

    public override void OnUpdate() {
        if (IsControlled || GameObject.Host == null || _hostChildren.Length == 0 || !ChildPlaces.Follows()) return;
        var places = new float[_hostChildren.Length * 2];
        for (var i = 0; i < _hostChildren.Length; i++) {
            var at = _hostChildren[i] != null ? _hostChildren[i].transform.localPosition : Vector3.zero;
            places[2 * i] = at.x;
            places[2 * i + 1] = at.y;
        }

        if (!ChildPlaces.Moved(_lastSent, places)) return;
        _lastSent = places;
        var data = new EntityNetworkData { Type = EntityComponentType.ChildPlaces };
        data.Packet.Write((byte) _hostChildren.Length);
        foreach (var value in places) data.Packet.Write(value);
        SendData(data);
    }

    protected override void InitializeHost() {
        _lastSent = null;
    }

    public override void Update(EntityNetworkData data, bool alreadyInSceneUpdate) {
        var count = data.Packet.ReadByte();
        var places = new float[count * 2];
        for (var i = 0; i < places.Length; i++) places[i] = data.Packet.ReadFloat();
        if (!IsControlled) return;
        for (var i = 0; i < count && i < _hostChildren.Length; i++) {
            Place(_hostChildren[i], places[2 * i], places[2 * i + 1]);
            var pair = _pairs[i];
            if (pair >= 0) Place(_clientChildren[pair], places[2 * i], places[2 * i + 1]);
        }
    }

    private static void Place(GameObject? child, float x, float y) {
        if (child == null) return;
        var at = child.transform.localPosition;
        child.transform.localPosition = new Vector3(x, y, at.z);
    }

    public override void Destroy() {
    }
}
