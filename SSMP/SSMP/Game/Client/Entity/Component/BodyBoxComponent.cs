using System;
using System.Collections.Generic;
using System.Reflection;
using MonoMod.RuntimeDetour;
using SSMP.Networking.Client;
using SSMP.Networking.Packet.Data;
using SSMP.Util;
using UnityEngine;

namespace SSMP.Game.Client.Entity.Component;

/// <summary>
/// (coop) An enemy's body box keeps the scene host's (BodyBox): its root's BoxCollider2D, enabled, size and offset,
/// sent when they change and set on the copy's every frame and again after the copy's own sprite has set it (the Bell
/// Beast's is her tk2d sprite's own box, sized and switched with each frame of her animation, which the copy plays from
/// its own start). (coop) With it the root's turn, held on the copy every frame: SSMP's rotation is set only as an update
/// arrives, and a turn replayed on the copy after the last one stayed for seconds under lag (335.8 degrees against the
/// host's 0). Requested by the entity registry (the Bell Beast).
/// </summary>
internal class BodyBoxComponent : EntityComponent {
    /// <summary>
    /// The copies whose body box this follows (their sprites' instance ids): tk2d's own collider update is skipped on
    /// them, as it set each new frame's box after the follow had (a 1 s stretch under lag 50, NOTES 339).
    /// </summary>
    private static readonly HashSet<int> Followed = [];

    private static Hook? _updateColliderHook;

    private static void UpdateCollider(Action<tk2dSprite> orig, tk2dSprite self) {
        if (self != null && Followed.Contains(self.GetInstanceID())) return;
        orig(self);
    }

    private tk2dSprite? _clientSprite;
    private BoxCollider2D? _host;
    private BoxCollider2D? _client;
    private float[]? _lastSent;
    private float[]? _given;

    public BodyBoxComponent(NetClient netClient, ushort entityId, HostClientPair<GameObject> gameObject)
        : base(netClient, entityId, gameObject) {
        MonoBehaviourUtil.Instance.OnLateUpdateEvent += OnLateUpdate;
        if (_updateColliderHook == null) {
            var method = typeof(tk2dSprite).GetMethod("UpdateCollider",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method != null) _updateColliderHook = new Hook(method, UpdateCollider);
        }
    }

    public override void OnUpdate() {
        if (IsControlled) {
            Apply();
            return;
        }

        // the box is the tk2d sprite's own, which it makes once it first sets a frame: looked up until found
        if (_host == null && GameObject.Host != null) _host = GameObject.Host.GetComponent<BoxCollider2D>();
        if (_host == null || !BodyBox.Follows()) return;
        var size = _host.size;
        var offset = _host.offset;
        float[] box = [_host.enabled ? 1f : 0f, size.x, size.y, offset.x, offset.y, _host.transform.eulerAngles.z];
        if (!BodyBox.Changed(_lastSent, box)) return;
        _lastSent = box;
        var data = new EntityNetworkData { Type = EntityComponentType.BodyBox };
        foreach (var value in box) data.Packet.Write(value);
        SendData(data);
    }

    private void OnLateUpdate() {
        if (IsControlled) Apply();
    }

    /// <summary>The copy's box as the scene host's last sent one.</summary>
    private void Apply() {
        if (_client == null && GameObject.Client != null) _client = GameObject.Client.GetComponent<BoxCollider2D>();
        if (_client == null && _given != null && _given[0] > 0.5f && GameObject.Client != null) {
            // the copy's sprite has not made its box yet while the scene host's hurts: made here, set as its original's
            var model = GameObject.Host != null ? GameObject.Host.GetComponent<BoxCollider2D>() : null;
            _client = GameObject.Client.AddComponent<BoxCollider2D>();
            if (model != null) _client.isTrigger = model.isTrigger;
        }

        if (_client == null || _given == null) return;
        if (_clientSprite == null && GameObject.Client != null && GameObject.Client.TryGetComponent(out tk2dSprite sprite)) {
            _clientSprite = sprite;
            Followed.Add(sprite.GetInstanceID());
        }

        var on = _given[0] > 0.5f;
        if (_client.enabled != on) _client.enabled = on;
        _client.size = new Vector2(_given[1], _given[2]);
        _client.offset = new Vector2(_given[3], _given[4]);
        var turn = _client.transform.eulerAngles;
        if (Mathf.Abs(Mathf.DeltaAngle(turn.z, _given[5])) > 0.01f) {
            _client.transform.eulerAngles = new Vector3(turn.x, turn.y, _given[5]);
        }
    }

    protected override void InitializeHost() {
        _lastSent = null;
        Unfollow();
    }

    /// <summary>The copy's own sprite sets its box again (this game runs the enemy now, or the copy is gone).</summary>
    private void Unfollow() {
        if (_clientSprite != null) Followed.Remove(_clientSprite.GetInstanceID());
        _clientSprite = null;
        _given = null;
    }

    public override void Update(EntityNetworkData data, bool alreadyInSceneUpdate) {
        var box = new float[6];
        for (var i = 0; i < box.Length; i++) box[i] = data.Packet.ReadFloat();
        if (!IsControlled) return;
        _given = box;
        Apply();
    }

    public override void Destroy() {
        MonoBehaviourUtil.Instance.OnLateUpdateEvent -= OnLateUpdate;
        Unfollow();
    }
}
