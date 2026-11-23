using SSMP.Networking.Client;
using SSMP.Networking.Packet.Data;
using UnityEngine;

namespace SSMP.Game.Client.Entity.Component;

// TODO: optimization idea: only add this component to objects where the FSM has an action that enables/disables the
// mesh renderer

/// <inheritdoc />
/// This component manages the mesh renderer of the entity.
internal class MeshRendererComponent : EntityComponent {
    /// <summary>
    /// The host-client pair of <see cref="MeshRenderer"/> unity components of the entity.
    /// </summary>
    private readonly HostClientPair<MeshRenderer> _meshRenderer;

    /// <summary>
    /// The last value of 'enabled' for the mesh renderer.
    /// </summary>
    private bool _lastEnabled;

    public MeshRendererComponent(
        NetClient netClient,
        ushort entityId,
        HostClientPair<GameObject> gameObject,
        HostClientPair<MeshRenderer> meshRenderer
    ) : base(netClient, entityId, gameObject) {
        _meshRenderer = meshRenderer;
        _lastEnabled = meshRenderer.Host.enabled;
    }

    /// <summary>
    /// Callback method to check for mesh renderer updates.
    /// </summary>
    /// <inheritdoc />
    public override void OnUpdate() {
        if (IsControlled) {
            return;
        }

        if (GameObject.Host == null) {
            return;
        }

        var newEnabled = _meshRenderer.Host.enabled;
        if (newEnabled != _lastEnabled) {
            _lastEnabled = newEnabled;

            var data = new EntityNetworkData {
                Type = EntityComponentType.MeshRenderer
            };
            data.Packet.Write(newEnabled);

            SendData(data);
        }
    }

    /// <inheritdoc />
    protected override void InitializeHost() {
        var enabled = _meshRenderer.Host.enabled;
        _lastEnabled = enabled;

        var data = new EntityNetworkData {
            Type = EntityComponentType.MeshRenderer
        };
        data.Packet.Write(enabled);
        SendData(data);
    }

    /// <inheritdoc />
    public override void Update(EntityNetworkData data, bool alreadyInSceneUpdate) => SetEnabled(data.Packet.ReadBool());

    /// <summary>(coop) Sets both renderers' enabled: an update's, or one Entity held with a hide that waited.</summary>
    internal void SetEnabled(bool enabled) {
        if (_meshRenderer.Host != null) {
            _meshRenderer.Host.enabled = enabled;
        }

        if (_meshRenderer.Client != null) {
            _meshRenderer.Client.enabled = enabled;
        }
    }

    /// <summary>(coop, 0.4) A taken-over original drawn as its copy was (TakeoverStates.Drawn).</summary>
    internal void DrawAsCopy() {
        if (_meshRenderer.Host != null && _meshRenderer.Client != null) {
            _meshRenderer.Host.enabled = TakeoverStates.Drawn(_meshRenderer.Client.enabled, _meshRenderer.Host.enabled);
        }
    }

    /// <inheritdoc />
    public override void Destroy() {
    }
}
